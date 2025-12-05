using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

/// <summary>
/// Service for matching weighing records and creating waybills
/// </summary>
public class WeighingMatchingService : DomainService
{
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly WeighingConfiguration _configuration;
    private readonly ILogger<WeighingMatchingService>? _logger;

    public WeighingMatchingService(
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IRepository<Waybill, long> waybillRepository,
        IUnitOfWorkManager unitOfWorkManager,
        IConfiguration configuration,
        ILogger<WeighingMatchingService>? logger = null)
    {
        _weighingRecordRepository = weighingRecordRepository;
        _waybillRepository = waybillRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;

        // Load configuration
        var configSection = configuration.GetSection("Weighing");
        _configuration = configSection.Get<WeighingConfiguration>() ?? new WeighingConfiguration();
    }

    /// <summary>
    /// Try to match weighing records and create waybills
    /// </summary>
    /// <param name="deliveryType">Delivery type (Delivery or Receiving) for weight relationship validation</param>
    /// <returns>Number of waybills created</returns>
    public async Task<int> TryMatchAndCreateWaybillsAsync(DeliveryType deliveryType)
    {
        try
        {
            // Get all unmatched weighing records
            var allRecords = await _weighingRecordRepository.GetListAsync();
            var unmatchedRecords = allRecords
                .Where(r => r.MatchedId == null)
                .OrderBy(r => r.CreationTime)
                .ToList();

            if (unmatchedRecords.Count < 2)
            {
                return 0; // Need at least 2 records to match
            }

            // Group by plate number (including empty/null as same group)
            var recordsByPlate = unmatchedRecords
                .GroupBy(r => r.PlateNumber ?? string.Empty)
                .ToList();

            int waybillsCreated = 0;

            foreach (var plateGroup in recordsByPlate)
            {
                var records = plateGroup.OrderBy(r => r.CreationTime).ToList();

                // Find matching pairs using rule 1 (same plate + time window)
                var matchingPairs = FindMatchingPairsByRule1(records);

                foreach (var pair in matchingPairs)
                {
                    // Validate rule 2 (time order + weight relationship)
                    if (ValidateRule2(pair.Item1, pair.Item2, deliveryType))
                    {
                        // Create waybill
                        await CreateWaybillAsync(pair.Item1, pair.Item2, deliveryType);
                        waybillsCreated++;
                    }
                }
            }

            return waybillsCreated;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "WeighingMatchingService: Error matching records and creating waybills");
            throw;
        }
    }

    /// <summary>
    /// Find matching pairs by rule 1: same plate number + time window
    /// If multiple candidates exist, select the pair with shortest time interval
    /// </summary>
    private List<(WeighingRecord, WeighingRecord)> FindMatchingPairsByRule1(List<WeighingRecord> records)
    {
        var pairs = new List<(WeighingRecord, WeighingRecord)>();
        var matchedRecordIds = new HashSet<long>();

        for (int i = 0; i < records.Count; i++)
        {
            if (matchedRecordIds.Contains(records[i].Id))
                continue;

            var candidatePairs = new List<(WeighingRecord, WeighingRecord, TimeSpan)>();

            for (int j = i + 1; j < records.Count; j++)
            {
                if (matchedRecordIds.Contains(records[j].Id))
                    continue;

                var timeDiff = records[j].CreationTime - records[i].CreationTime;
                var timeWindow = TimeSpan.FromHours(_configuration.WeighingMatchDurationHours);

                // Check if time difference is within window
                if (timeDiff <= timeWindow && timeDiff >= TimeSpan.Zero)
                {
                    candidatePairs.Add((records[i], records[j], timeDiff));
                }
            }

            // If multiple candidates, select the one with shortest time interval
            if (candidatePairs.Any())
            {
                var bestPair = candidatePairs.OrderBy(p => p.Item3).First();
                pairs.Add((bestPair.Item1, bestPair.Item2));
                matchedRecordIds.Add(bestPair.Item1.Id);
                matchedRecordIds.Add(bestPair.Item2.Id);
            }
        }

        return pairs;
    }

    /// <summary>
    /// Validate rule 2: time order + weight relationship based on DeliveryType
    /// - Join record must be created before Out record
    /// - For Delivery: Join.Weight < Out.Weight
    /// - For Receiving: Join.Weight > Out.Weight
    /// </summary>
    private bool ValidateRule2(WeighingRecord record1, WeighingRecord record2, DeliveryType deliveryType)
    {
        // Determine which is Join (earlier) and which is Out (later)
        WeighingRecord joinRecord, outRecord;
        if (record1.CreationTime < record2.CreationTime)
        {
            joinRecord = record1;
            outRecord = record2;
        }
        else
        {
            joinRecord = record2;
            outRecord = record1;
        }

        // Validate weight relationship based on DeliveryType
        if (deliveryType == DeliveryType.Delivery)
        {
            // 发料: Join.Weight < Out.Weight
            return joinRecord.Weight < outRecord.Weight;
        }
        else // Receiving
        {
            // 收料: Join.Weight > Out.Weight
            return joinRecord.Weight > outRecord.Weight;
        }
    }

    /// <summary>
    /// Create waybill from matched weighing records
    /// </summary>
    private async Task CreateWaybillAsync(WeighingRecord record1, WeighingRecord record2, DeliveryType deliveryType)
    {
        using var uow = _unitOfWorkManager.Begin();

        // Determine Join and Out records
        WeighingRecord joinRecord, outRecord;
        if (record1.CreationTime < record2.CreationTime)
        {
            joinRecord = record1;
            outRecord = record2;
        }
        else
        {
            joinRecord = record2;
            outRecord = record1;
        }

        // Create waybill
        var orderNo = Guid.NewGuid().ToString(); // Generate OrderNo from Guid
        var waybill = new Waybill(orderNo) // Id will be auto-generated
        {
            PlateNumber = joinRecord.PlateNumber ?? outRecord.PlateNumber,
            JoinTime = joinRecord.CreationTime,
            OutTime = outRecord.CreationTime,
            DeliveryType = (int)deliveryType,
            OrderSource = OrderSource.MannedStation,
            OrderTruckWeight = joinRecord.Weight,
            OrderTotalWeight = outRecord.Weight,
            OrderGoodsWeight = outRecord.Weight - joinRecord.Weight
        };

        // If ProviderId is 0 (both records had null ProviderId), set to nullable
        // But Waybill.ProviderId is int (not nullable), so we keep 0 and let business logic handle it
        if (waybill.ProviderId == 0)
        {
            // TODO: Handle null Provider case - may need to make ProviderId nullable in Waybill
            _logger?.LogWarning($"WeighingMatchingService: Created waybill with ProviderId=0 (both records had null ProviderId)");
        }

        await _waybillRepository.InsertAsync(waybill);
        await uow.CompleteAsync();

        // Update WeighingRecord types
        using var uow2 = _unitOfWorkManager.Begin();

        await _weighingRecordRepository.UpdateAsync(joinRecord);
        await _weighingRecordRepository.UpdateAsync(outRecord);
        await uow2.CompleteAsync();

        _logger?.LogInformation($"WeighingMatchingService: Created waybill {waybill.OrderNo} from records {joinRecord.Id} (Join) and {outRecord.Id} (Out)");
    }
}

