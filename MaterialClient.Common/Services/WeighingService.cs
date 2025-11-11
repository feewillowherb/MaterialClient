using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.Hardware;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

/// <summary>
/// Weighing service for monitoring truck scale and creating weighing records
/// </summary>
public class WeighingService : DomainService
{
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly IPlateNumberCaptureService _plateNumberCaptureService;
    private readonly IVehiclePhotoService _vehiclePhotoService;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<WeighingRecordAttachment, int> _weighingRecordAttachmentRepository;
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly Lazy<WeighingMatchingService> _matchingService;
    private readonly WeighingConfiguration _configuration;
    private readonly ILogger<WeighingService>? _logger;

    private VehicleWeightStatus _currentStatus = VehicleWeightStatus.OffScale;
    private decimal _lastWeight = 0m;
    private DateTime _stableStartTime;
    private Timer? _monitoringTimer;
    private readonly object _lockObject = new();

    public WeighingService(
        ITruckScaleWeightService truckScaleWeightService,
        IPlateNumberCaptureService plateNumberCaptureService,
        IVehiclePhotoService vehiclePhotoService,
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IRepository<WeighingRecordAttachment, int> weighingRecordAttachmentRepository,
        IRepository<AttachmentFile, int> attachmentFileRepository,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<WeighingService>? logger = null)
    {
        _truckScaleWeightService = truckScaleWeightService;
        _plateNumberCaptureService = plateNumberCaptureService;
        _vehiclePhotoService = vehiclePhotoService;
        _weighingRecordRepository = weighingRecordRepository;
        _weighingRecordAttachmentRepository = weighingRecordAttachmentRepository;
        _attachmentFileRepository = attachmentFileRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;

        // Lazy load matching service to avoid circular dependency
        _matchingService = new Lazy<WeighingMatchingService>(() => 
            serviceProvider.GetRequiredService<WeighingMatchingService>());

        // Load configuration
        var configSection = configuration.GetSection("Weighing");
        _configuration = configSection.Get<WeighingConfiguration>() ?? new WeighingConfiguration();
    }

    /// <summary>
    /// Start monitoring truck scale weight
    /// </summary>
    public void StartMonitoring()
    {
        lock (_lockObject)
        {
            if (_monitoringTimer != null)
            {
                return; // Already monitoring
            }

            // Check weight every 500ms
            _monitoringTimer = new Timer(CheckWeight, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
            _logger?.LogInformation("WeighingService: Started monitoring truck scale");
        }
    }

    /// <summary>
    /// Stop monitoring truck scale weight
    /// </summary>
    public void StopMonitoring()
    {
        lock (_lockObject)
        {
            _monitoringTimer?.Dispose();
            _monitoringTimer = null;
            _logger?.LogInformation("WeighingService: Stopped monitoring truck scale");
        }
    }

    /// <summary>
    /// Get current vehicle weight status
    /// </summary>
    public VehicleWeightStatus GetCurrentStatus()
    {
        lock (_lockObject)
        {
            return _currentStatus;
        }
    }

    private void CheckWeight(object? state)
    {
        try
        {
            var currentWeight = _truckScaleWeightService.GetCurrentWeightAsync().GetAwaiter().GetResult();
            ProcessWeightChange(currentWeight);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "WeighingService: Error checking weight");
        }
    }

    private void ProcessWeightChange(decimal currentWeight)
    {
        lock (_lockObject)
        {
            var weightChange = currentWeight - _lastWeight;
            _lastWeight = currentWeight;

            // Check if weight is within offset range
            var isWithinOffsetRange = currentWeight >= _configuration.WeightOffsetRangeMin &&
                                     currentWeight <= _configuration.WeightOffsetRangeMax;

            // Check if weight exceeds offset range (boundary values are considered "exceeded")
            var exceedsOffsetRange = currentWeight > _configuration.WeightOffsetRangeMax ||
                                    currentWeight < _configuration.WeightOffsetRangeMin;

            switch (_currentStatus)
            {
                case VehicleWeightStatus.OffScale:
                    // Transition: OffScale -> OnScale when weight exceeds offset range
                    if (exceedsOffsetRange)
                    {
                        _currentStatus = VehicleWeightStatus.OnScale;
                        _stableStartTime = DateTime.UtcNow;
                        _logger?.LogInformation($"WeighingService: Vehicle on scale. Weight: {currentWeight} kg");
                    }
                    break;

                case VehicleWeightStatus.OnScale:
                    // Check if weight is stable and exceeds offset range
                    if (exceedsOffsetRange)
                    {
                        var stableDuration = (DateTime.UtcNow - _stableStartTime).TotalMilliseconds;
                        if (stableDuration >= _configuration.WeightStableDurationMs)
                        {
                            // Transition: OnScale -> Weighing (stable weight)
                            _currentStatus = VehicleWeightStatus.Weighing;
                            _ = Task.Run(() => CreateWeighingRecordAsync(currentWeight));
                        }
                    }
                    else
                    {
                        // Weight went back within offset range before stabilizing
                        // Transition: OnScale -> OffScale
                        _currentStatus = VehicleWeightStatus.OffScale;
                        _logger?.LogWarning("WeighingService: Vehicle left scale before weighing completed");
                    }
                    break;

                case VehicleWeightStatus.Weighing:
                    // Wait for vehicle to leave scale
                    if (isWithinOffsetRange)
                    {
                        // Transition: Weighing -> OffScale
                        _currentStatus = VehicleWeightStatus.OffScale;
                        _logger?.LogInformation("WeighingService: Vehicle left scale, ready for next vehicle");
                    }
                    break;
            }
        }
    }

    private async Task CreateWeighingRecordAsync(decimal weight)
    {
        try
        {
            using var uow = _unitOfWorkManager.Begin();
            
            // Create weighing record
            var weighingRecord = new WeighingRecord(0, weight) // Id will be auto-generated
            {
                PlateNumber = null, // Will be set if capture succeeds
                RecordType = WeighingRecordType.Unmatch
            };

            // Try to capture plate number
            try
            {
                var plateNumber = await _plateNumberCaptureService.CapturePlateNumberAsync();
                weighingRecord.PlateNumber = plateNumber;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "WeighingService: Failed to capture plate number, continuing without it");
            }

            // Save weighing record
            await _weighingRecordRepository.InsertAsync(weighingRecord);
            await uow.CompleteAsync();

            _logger?.LogInformation($"WeighingService: Created weighing record {weighingRecord.Id} with weight {weight} kg");

            // Try to capture vehicle photos
            try
            {
                var photoPaths = await _vehiclePhotoService.CaptureVehiclePhotosAsync();
                await SaveVehiclePhotosAsync(weighingRecord.Id, photoPaths);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "WeighingService: Failed to capture vehicle photos, continuing without them");
            }

            // Try to match with existing records and create waybills
            // Use default DeliveryType (Delivery) - can be configured later
            try
            {
                var waybillsCreated = await _matchingService.Value.TryMatchAndCreateWaybillsAsync(DeliveryType.Delivery);
                if (waybillsCreated > 0)
                {
                    _logger?.LogInformation($"WeighingService: Created {waybillsCreated} waybill(s) after matching");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "WeighingService: Failed to match records, will retry later");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "WeighingService: Failed to create weighing record");
        }
    }

    private async Task SaveVehiclePhotosAsync(long weighingRecordId, List<string> photoPaths)
    {
        try
        {
            using var uow = _unitOfWorkManager.Begin();

            foreach (var photoPath in photoPaths)
            {
                // Create attachment file
                var fileName = Path.GetFileName(photoPath);
                var attachmentFile = new AttachmentFile(0, fileName, photoPath, AttachType.EntryPhoto) // Id will be auto-generated
                {
                    // Vehicle photos are entry photos
                };

                await _attachmentFileRepository.InsertAsync(attachmentFile);

                // Create weighing record attachment
                var weighingRecordAttachment = new WeighingRecordAttachment(0, weighingRecordId, attachmentFile.Id); // Id will be auto-generated

                await _weighingRecordAttachmentRepository.InsertAsync(weighingRecordAttachment);
            }

            await uow.CompleteAsync();
            _logger?.LogInformation($"WeighingService: Saved {photoPaths.Count} vehicle photos for weighing record {weighingRecordId}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"WeighingService: Failed to save vehicle photos for weighing record {weighingRecordId}");
            throw;
        }
    }
}

