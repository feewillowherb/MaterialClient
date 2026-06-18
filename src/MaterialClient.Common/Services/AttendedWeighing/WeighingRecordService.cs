using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services.Urban;
using MaterialClient.Common.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services.AttendedWeighing;

/// <summary>
///     称重记录服务接口
/// </summary>
public interface IWeighingRecordService
{
    /// <summary>
    ///     创建称重记录
    /// </summary>
    Task CreateWeighingRecordAsync(decimal weight, List<string> photoPaths, WeighingStateManager stateManager);

    /// <summary>
    ///     保存抓拍的照片
    /// </summary>
    Task SaveCapturePhotosAsync(long weighingRecordId, List<string> photoPaths);

    /// <summary>
    ///     尝试重写称重记录的车牌号和收发类型
    /// </summary>
    Task TryReWritePlateNumberAsync(WeighingStateManager stateManager);

    /// <summary>
    ///     重写车牌并重置周期
    /// </summary>
    Task RewriteAndResetCycleAsync(WeighingStateManager stateManager, IPlateNumberService plateNumberService);

    /// <summary>
    ///     更新称重记录的车牌号和重量，并重置关联的 UrbanWeighingExtension 同步状态为 Pending
    ///     同时更新异常标志以反映编辑后的记录状态
    /// </summary>
    Task UpdateWeighingRecordAsync(long weighingRecordId, string plateNumber, decimal totalWeight);
}

/// <summary>
///     称重记录服务
///     处理 WeighingRecord 创建、车牌号重写、照片附件保存和 TryMatch 事件发布
/// </summary>
public class WeighingRecordService : IWeighingRecordService, ISingletonDependency
{
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IUrbanWeighingExtensionService _urbanWeighingExtensionService;
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;
    private readonly IRepository<WeighingRecordAttachment, int> _weighingRecordAttachmentRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ISettingsService _settingsService;
    private readonly IPlateNumberService _plateNumberService;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<WeighingRecordService> _logger;
    private readonly IWeighingPipelineStrategy _pipelineStrategy;
    private readonly IUrbanAnomalyDetector _anomalyDetector;
    private readonly IConfiguration _configuration;

    public WeighingRecordService(
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IUrbanWeighingExtensionService urbanWeighingExtensionService,
        IRepository<AttachmentFile, int> attachmentFileRepository,
        IRepository<WeighingRecordAttachment, int> weighingRecordAttachmentRepository,
        IUnitOfWorkManager unitOfWorkManager,
        ISettingsService settingsService,
        IPlateNumberService plateNumberService,
        ILocalEventBus localEventBus,
        ILogger<WeighingRecordService> logger,
        IUrbanAnomalyDetector anomalyDetector,
        IConfiguration configuration,
        IWeighingPipelineStrategy? pipelineStrategy = null)
    {
        _weighingRecordRepository = weighingRecordRepository;
        _urbanWeighingExtensionService = urbanWeighingExtensionService;
        _attachmentFileRepository = attachmentFileRepository;
        _weighingRecordAttachmentRepository = weighingRecordAttachmentRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _settingsService = settingsService;
        _plateNumberService = plateNumberService;
        _localEventBus = localEventBus;
        _logger = logger;
        _anomalyDetector = anomalyDetector;
        _configuration = configuration;
        _pipelineStrategy = pipelineStrategy ?? new DefaultWeighingPipelineStrategy(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultWeighingPipelineStrategy>());
    }

    /// <inheritdoc />
    public async Task CreateWeighingRecordAsync(decimal weight, List<string> photoPaths,
        WeighingStateManager stateManager)
    {
        try
        {
            var plateNumber = _plateNumberService.GetMostFrequentPlateNumber();

            using var uow = _unitOfWorkManager.Begin();

            var currentDeliveryType = stateManager.CurrentDeliveryType;
            var weighingRecord = new WeighingRecord(weight, plateNumber);
            weighingRecord.DeliveryType = currentDeliveryType;

            // 获取并设置车辆信息
            var (vehicleColor, vehicleType, plateColor) = stateManager.GetCurrentCycleVehicleInfo();
            weighingRecord.VehicleColor = vehicleColor;
            weighingRecord.VehicleType = vehicleType;
            weighingRecord.PlateColor = plateColor;

            if (!string.IsNullOrWhiteSpace(vehicleColor) || !string.IsNullOrWhiteSpace(vehicleType) ||
                !string.IsNullOrWhiteSpace(plateColor))
                _logger.LogDebug(
                    "Vehicle info attached to record: VehicleColor={VehicleColor}, VehicleType={VehicleType}, PlateColor={PlateColor}",
                    vehicleColor, vehicleType, plateColor);

            var weighingMode = await _settingsService.GetWeighingModeAsync();
            weighingRecord.SetWeighingMode(weighingMode);

            await _weighingRecordRepository.InsertAsync(weighingRecord, autoSave: true);

            if (weighingMode == WeighingMode.UrbanMode)
            {
                var hasLrp = !string.IsNullOrWhiteSpace(stateManager.GetCurrentCycleLprImagePath());
                var extension = await _urbanWeighingExtensionService.CreateForRecordAsync(weighingRecord.Id, hasLrp);
            }

            await uow.CompleteAsync();

            _logger.LogInformation(
                "Created weighing record successfully, ID: {Id}, Weight: {Weight}t, PlateNumber: {PlateNumber}, DeliveryType: {DeliveryType}",
                weighingRecord.Id, weight, plateNumber ?? "None", currentDeliveryType);

            // Save last created weighing record ID for later rewrite
            stateManager.SetLastCreatedWeighingRecordId(weighingRecord.Id);

            // Notify observers via ILocalEventBus
            _ = _localEventBus.PublishAsync(new WeighingRecordCreatedEventData(weighingRecord.Id));

            // Save captured photos
            if (photoPaths.Count > 0)
                await SaveCapturePhotosAsync(weighingRecord.Id, photoPaths);
            else
                _logger.LogWarning("Weighing record {Id} has no associated photos", weighingRecord.Id);

            if (weighingMode == WeighingMode.UrbanMode)
                await SaveLprAttachmentAsync(weighingRecord.Id, stateManager.GetCurrentCycleLprImagePath());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while creating weighing record");
        }
    }

    /// <inheritdoc />
    public async Task SaveCapturePhotosAsync(long weighingRecordId, List<string> photoPaths)
    {
        try
        {
            if (photoPaths.Count == 0) return;

            var weighingMode = await _settingsService.GetWeighingModeAsync();
            var attachType = weighingMode == WeighingMode.UrbanMode
                ? AttachType.UrbanPhoto
                : AttachType.UnmatchedEntryPhoto;

            using var uow = _unitOfWorkManager.Begin();

            foreach (var photoPath in photoPaths)
                try
                {
                    if (!File.Exists(photoPath))
                    {
                        _logger.LogWarning("Photo file does not exist: {PhotoPath}", photoPath);
                        continue;
                    }

                    var fileName = Path.GetFileName(photoPath);
                    var relativePath = PathManager.ToRelativePath(photoPath);
                    var attachmentFile = new AttachmentFile(fileName, relativePath, attachType);

                    await _attachmentFileRepository.InsertAsync(attachmentFile, true);

                    var weighingRecordAttachment = new WeighingRecordAttachment(weighingRecordId, attachmentFile.Id);
                    await _weighingRecordAttachmentRepository.InsertAsync(weighingRecordAttachment, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save photo: {PhotoPath}", photoPath);
                }

            await uow.CompleteAsync();
            _logger.LogInformation("Saved {Count} photos ({AttachType}) to weighing record {Id}", photoPaths.Count,
                attachType, weighingRecordId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while saving captured photos");
        }
    }

    private async Task SaveLprAttachmentAsync(long weighingRecordId, string? lrpRelativePath)
    {
        if (string.IsNullOrWhiteSpace(lrpRelativePath))
        {
            _logger.LogDebug("No Lpr image path for weighing record {Id}, skipping Lrp attachment", weighingRecordId);
            return;
        }

        try
        {
            if (!AttachmentPathUtils.FileExists(lrpRelativePath))
            {
                _logger.LogWarning("Lpr photo file does not exist: {PhotoPath}", lrpRelativePath);
                return;
            }

            using var uow = _unitOfWorkManager.Begin();

            var fileName = Path.GetFileName(lrpRelativePath);
            var attachmentFile = new AttachmentFile(fileName, lrpRelativePath, AttachType.Lpr);
            await _attachmentFileRepository.InsertAsync(attachmentFile, true);

            var weighingRecordAttachment = new WeighingRecordAttachment(weighingRecordId, attachmentFile.Id);
            await _weighingRecordAttachmentRepository.InsertAsync(weighingRecordAttachment, true);

            await uow.CompleteAsync();
            _logger.LogInformation("Saved Lrp attachment to weighing record {Id}: {Path}", weighingRecordId,
                lrpRelativePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while saving Lrp attachment for record {Id}", weighingRecordId);
        }
    }

    /// <inheritdoc />
    public async Task TryReWritePlateNumberAsync(WeighingStateManager stateManager)
    {
        var recordId = stateManager.GetLastCreatedWeighingRecordId();

        try
        {
            if (recordId == null)
            {
                _logger.LogDebug("No recent weighing record to rewrite plate number");
                return;
            }

            var config = await GetConfigurationAsync();

            using var uow = _unitOfWorkManager.Begin();
            var weighingRecord = await _weighingRecordRepository.GetAsync(recordId.Value);

            var currentDeliveryType = stateManager.CurrentDeliveryType;
            var hasChanges = false;

            if (config.EnablePlateRewrite)
            {
                var plateNumber = _plateNumberService.GetMostFrequentPlateNumber();
                if (!string.IsNullOrWhiteSpace(plateNumber) && weighingRecord.PlateNumber != plateNumber)
                {
                    var oldPlateNumber = weighingRecord.PlateNumber;
                    weighingRecord.PlateNumber = plateNumber;
                    hasChanges = true;

                    _logger.LogInformation(
                        "Rewrote plate number for weighing record {Id}, from '{OldPlate}' to '{NewPlate}'",
                        weighingRecord.Id, oldPlateNumber ?? "None", plateNumber);

                    var updateEventData = new UpdatePlateNumberEventData(weighingRecord.Id, plateNumber);
                    _ = _localEventBus.PublishAsync(updateEventData);
                }
            }
            else
            {
                _logger.LogDebug("Plate number rewrite is disabled, skipping plate number update");
            }

            if (weighingRecord.DeliveryType != currentDeliveryType)
            {
                var oldDeliveryType = weighingRecord.DeliveryType;
                weighingRecord.DeliveryType = currentDeliveryType;
                hasChanges = true;

                _logger.LogInformation(
                    "Rewrote delivery type for weighing record {Id}, from '{OldType}' to '{NewType}'",
                    weighingRecord.Id, oldDeliveryType, currentDeliveryType);
            }

            if (hasChanges)
            {
                await _weighingRecordRepository.UpdateAsync(weighingRecord);
                await uow.CompleteAsync();

                if (!_pipelineStrategy.ShouldSkipWaybillMatching())
                {
                    await _localEventBus.PublishAsync(new TryMatchEvent(weighingRecord.Id));
                }
                else
                {
                    _logger.LogDebug("UrbanMode: 跳过 TryMatchEvent 发布 for record {Id}", weighingRecord.Id);
                }
            }
            else
            {
                await uow.CompleteAsync();
                _logger.LogDebug("Plate number and delivery type unchanged for weighing record {Id}",
                    recordId.Value);

                if (!_pipelineStrategy.ShouldSkipWaybillMatching())
                {
                    await _localEventBus.PublishAsync(new TryMatchEvent(weighingRecord.Id));
                }
                else
                {
                    _logger.LogDebug("UrbanMode: 跳过 TryMatchEvent 发布 for record {Id}", weighingRecord.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while rewriting plate number");
        }
    }

    /// <inheritdoc />
    public async Task RewriteAndResetCycleAsync(WeighingStateManager stateManager,
        IPlateNumberService plateNumberService)
    {
        await TryReWritePlateNumberAsync(stateManager);
        plateNumberService.ClearCache();
        stateManager.ResetCycle();
    }

    /// <inheritdoc />
    [UnitOfWork]
    public virtual async Task UpdateWeighingRecordAsync(long weighingRecordId, string plateNumber, decimal totalWeight)
    {
        var record = await _weighingRecordRepository.GetAsync(weighingRecordId);

        record.PlateNumber = plateNumber;
        record.TotalWeight = totalWeight;

        await _weighingRecordRepository.UpdateAsync(record);

        var extension = await _urbanWeighingExtensionService.GetByWeighingRecordIdAsync(weighingRecordId);
        if (extension != null)
        {
            await _urbanWeighingExtensionService.UpdateSyncStatusAsync(extension.Id, Entities.Enums.SyncStatus.Pending);

            // Anomaly detection integration: recalculate anomaly flag after record edit
            // This ensures the anomaly status stays in sync with the edited record data
            var anomalyConfig = GetAnomalyDetectionConfig();
            var hasLpr = await HasLprAttachmentAsync(weighingRecordId);
            var isAnomaly = _anomalyDetector.IsAnomaly(record, anomalyConfig, hasLpr);
            var reason = isAnomaly ? _anomalyDetector.GetAnomalyReason(record, anomalyConfig, hasLpr) : null;
            await _urbanWeighingExtensionService.UpdateAnomalyStateAsync(extension.Id, isAnomaly, reason);

            _logger.LogInformation(
                "Updated weighing record {Id}: PlateNumber={PlateNumber}, TotalWeight={TotalWeight}, SyncStatus reset to Pending, IsAnomaly={IsAnomaly}",
                weighingRecordId, plateNumber, totalWeight, isAnomaly);
        }
        else
        {
            _logger.LogInformation(
                "Updated weighing record {Id}: PlateNumber={PlateNumber}, TotalWeight={TotalWeight}, SyncStatus reset to Pending (no extension found)",
                weighingRecordId, plateNumber, totalWeight);
        }
    }

    private async Task<WeighingConfiguration> GetConfigurationAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            return settings.WeighingConfiguration;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load configuration, using default values");
            return new WeighingConfiguration();
        }
    }

    /// <summary>
    ///     Reads UrbanAnomalyDetection config from IConfiguration.
    ///     Falls back to defaults and logs a warning on failure.
    /// </summary>
    private UrbanAnomalyDetectionConfig GetAnomalyDetectionConfig()
    {
        try
        {
            var config = new UrbanAnomalyDetectionConfig();
            _configuration.GetSection("UrbanAnomalyDetection").Bind(config);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read UrbanAnomalyDetection config, using default values");
            return new UrbanAnomalyDetectionConfig();
        }
    }

    private async Task<bool> HasLprAttachmentAsync(long weighingRecordId)
    {
        var recordAttachments = await _weighingRecordAttachmentRepository.GetListAsync(a => a.WeighingRecordId == weighingRecordId);
        if (recordAttachments.Count == 0) return false;

        var fileIds = recordAttachments.Select(a => a.AttachmentFileId).ToList();
        var lprFiles = await _attachmentFileRepository.GetListAsync(f => fileIds.Contains(f.Id) && f.AttachType == AttachType.Lpr);
        return lprFiles.Count > 0;
    }
}
