using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

public interface IWeighingMatchingService
{
    Task<bool> TryMatchWeighingRecordAsync(WeighingRecord record);
    Task UpdateWaybillAsync(UpdateWaybillInput input);

    Task UpdateWeighingRecordAsync(UpdateWeighingRecordInput input);
}

/// <summary>
/// Service for matching weighing records and creating waybills
/// </summary>
[AutoConstructor]
public partial class WeighingMatchingService : DomainService, IWeighingMatchingService
{
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IRepository<WaybillMaterial, int> _waybillMaterialRepository;
    private readonly IRepository<Material, int> _materialRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly WeighingConfiguration _configuration;
    private readonly ILogger<WeighingMatchingService>? _logger;


    private const int MaxIntervalMinutes = 300;
    private const decimal MinTon = 1m;


    [UnitOfWork]
    public async Task<bool> TryMatchWeighingRecordAsync(WeighingRecord record)
    {
        if (!record.IsValidChinesePlateNumber())
        {
            _logger?.LogInformation(
                "WeighingMatchingService: Record {RecordId} has invalid plate number '{PlateNumber}', skipping matching",
                record.Id, record.PlateNumber);
            return false;
        }

        var query = await _weighingRecordRepository.GetQueryableAsync();

        var unmatchedRecords = await query
            .Where(r => r.PlateNumber == record.PlateNumber && r.Id != record.Id)
            .Where(r => r.MatchedId == null)
            .OrderByDescending(r => r.CreationTime)
            .ToListAsync();
        if (unmatchedRecords.Count == 0)
        {
            _logger?.LogInformation(
                "WeighingMatchingService: No unmatched records found for plate '{PlateNumber}' to match with record {RecordId}",
                record.PlateNumber, record.Id);
            return false;
        }

        var validTime = record.CreationTime.AddMinutes(-MaxIntervalMinutes);
        var timeoutOrders = unmatchedRecords
            .Where(r => r.CreationTime < validTime)
            .ToList();
        if (timeoutOrders.Any())
        {
            _logger?.LogInformation(
                "WeighingMatchingService: Found {Count} timeout unmatched records for plate '{PlateNumber}' to match with record {RecordId}",
                timeoutOrders.Count, record.PlateNumber, record.Id);
        }

        if (record.DeliveryType == null || record.DeliveryType == DeliveryType.Receiving)
        {
            var candidateRecords = unmatchedRecords
                .Where(r => r.CreationTime >= validTime)
                .Where(r => r.Weight > record.Weight)
                .Where(r => (r.Weight - record.Weight) > MinTon)
                .OrderByDescending(r => r.CreationTime)
                .ToList();
            if (candidateRecords.Count == 0)
            {
                _logger?.LogInformation(
                    "WeighingMatchingService: No candidate records found for matching with record {RecordId}",
                    record.Id);
                return false;
            }

            var matchedRecord = candidateRecords
                .OrderBy(r => r.CreationTime - record.CreationTime)
                .First();

            await CreateWaybillAsync(record, matchedRecord, DeliveryType.Receiving);
            return true;
        }

        if (record.DeliveryType == DeliveryType.Sending)
        {
            var candidateRecords = unmatchedRecords
                .Where(r => r.CreationTime >= validTime)
                .Where(r => r.Weight < record.Weight)
                .Where(r => (record.Weight - r.Weight) > MinTon)
                .OrderByDescending(r => r.CreationTime)
                .ToList();
            if (candidateRecords.Count == 0)
            {
                _logger?.LogInformation(
                    "WeighingMatchingService: No candidate records found for matching with record {RecordId}",
                    record.Id);
                return false;
            }

            var matchedRecord = candidateRecords
                .OrderBy(r => r.CreationTime - record.CreationTime)
                .First();

            await CreateWaybillAsync(matchedRecord, record, DeliveryType.Sending);
            return true;
        }

        _logger?.LogWarning(
            "WeighingMatchingService: Record {RecordId} has unknown DeliveryType '{DeliveryType}', skipping matching",
            record.Id, record.DeliveryType);
        return false;
    }

    [UnitOfWork]
    public async Task UpdateWaybillAsync(UpdateWaybillInput input)
    {
        var waybill = await _waybillRepository.FindAsync(input.WaybillId);

        if (waybill == null)
        {
            _logger?.LogError("Waybill with ID {WaybillId} not found.", input.WaybillId);
            return;
        }

        // 更新基本字段
        if (input.PlateNumber != null) waybill.PlateNumber = input.PlateNumber;
        if (input.ProviderId.HasValue) waybill.ProviderId = input.ProviderId.Value;

        // 计算物料信息
        await TryCalculateMaterialAsync(waybill,
            input.MaterialId ?? waybill.MaterialId,
            input.MaterialUnitId ?? waybill.MaterialUnitId,
            input.WaybillQuantity ?? waybill.OrderPlanOnPcs);

        await _waybillRepository.UpdateAsync(waybill);
    }

    [UnitOfWork]
    public async Task UpdateWeighingRecordAsync(UpdateWeighingRecordInput input)
    {
        var record = await _weighingRecordRepository.FindAsync(input.WeighingRecordId);

        if (record == null)
        {
            _logger?.LogError("WeighingRecord with ID {RecordId} not found.", input.WeighingRecordId);
            return;
        }

        // 更新字段
        if (input.PlateNumber != null) record.PlateNumber = input.PlateNumber;
        if (input.ProviderId.HasValue) record.ProviderId = input.ProviderId;
        if (input.MaterialId.HasValue) record.MaterialId = input.MaterialId;
        if (input.MaterialUnitId.HasValue) record.MaterialUnitId = input.MaterialUnitId;
        if (input.WaybillQuantity.HasValue) record.WaybillQuantity = input.WaybillQuantity;
        if (input.DeliveryType.HasValue) record.DeliveryType = input.DeliveryType;

        await _weighingRecordRepository.UpdateAsync(record);
    }

    private async Task TryCalculateMaterialAsync(Waybill waybill, int? materialId, int? materialUnitId,
        decimal? waybillQuantity)
    {
        if (!materialUnitId.HasValue || !waybillQuantity.HasValue || !materialId.HasValue)
            return;

        var material = await _materialRepository.GetAsync(materialId.Value);

        // 更新 Waybill 的物料信息
        waybill.MaterialId = materialId;
        waybill.MaterialUnitId = materialUnitId;
        waybill.OrderPlanOnPcs = waybillQuantity;
        waybill.MaterialUnitRate = material.UnitRate;
        waybill.CalculateMaterialWeight(material.LowerLimit, material.UpperLimit);

        // 查找或创建 WaybillMaterial
        var existingMaterial = await _waybillMaterialRepository.FirstOrDefaultAsync(
            wm => wm.WaybillId == waybill.Id);

        if (existingMaterial != null)
        {
            // 更新现有记录
            existingMaterial.MaterialId = material.Id;
            existingMaterial.MaterialName = material.Name;
            existingMaterial.Specifications = material.Specifications;
            existingMaterial.MaterialUnitId = materialUnitId;
            existingMaterial.GoodsPlanOnPcs = waybillQuantity.Value;
            existingMaterial.UpdateOffsetFromWaybill(waybill);
            await _waybillMaterialRepository.UpdateAsync(existingMaterial);
        }
        else
        {
            // 创建新记录
            var waybillMaterial = new WaybillMaterial(waybill.Id, material.Id, material.Name,
                material.Specifications, materialUnitId.Value, waybillQuantity.Value);
            waybillMaterial.UpdateOffsetFromWaybill(waybill);
            await _waybillMaterialRepository.InsertAsync(waybillMaterial);
        }
    }


    [UnitOfWork]
    private async Task CreateWaybillAsync(WeighingRecord joinRecord, WeighingRecord outRecord,
        DeliveryType deliveryType)
    {
        var todayCount = await _waybillRepository.CountAsync(w =>
            w.CreationTime.Date == DateTime.Today);

        var orderNo = Waybill.GenerateOrderNo(deliveryType, joinRecord.CreationTime, todayCount);
        var waybill = new Waybill(orderNo)
        {
            PlateNumber = joinRecord.PlateNumber ?? outRecord.PlateNumber,
            JoinTime = joinRecord.CreationTime,
            OutTime = outRecord.CreationTime,
            DeliveryType = deliveryType,
            OrderSource = OrderSource.MannedStation
        };
        waybill.SetWeight(joinRecord, outRecord, deliveryType);
        joinRecord.MatchAsJoin(outRecord.Id);
        outRecord.MatchAsOut(joinRecord.Id);

        // 先插入 Waybill 获取 Id
        await _waybillRepository.InsertAsync(waybill);

        // 计算物料信息
        var materialId = joinRecord.MaterialId ?? outRecord.MaterialId;
        var materialUnitId = joinRecord.MaterialUnitId ?? outRecord.MaterialUnitId;
        var waybillQuantity = joinRecord.WaybillQuantity ?? outRecord.WaybillQuantity;
        await TryCalculateMaterialAsync(waybill, materialId, materialUnitId, waybillQuantity);

        await _weighingRecordRepository.UpdateAsync(joinRecord);
        await _weighingRecordRepository.UpdateAsync(outRecord);
    }
}

public record UpdateWaybillInput(
    long WaybillId,
    string? PlateNumber,
    int? ProviderId,
    int? MaterialId,
    int? MaterialUnitId,
    decimal? WaybillQuantity
);

public record UpdateWeighingRecordInput(
    long WeighingRecordId,
    string? PlateNumber,
    int? ProviderId,
    int? MaterialId,
    int? MaterialUnitId,
    decimal? WaybillQuantity,
    DeliveryType? DeliveryType
);