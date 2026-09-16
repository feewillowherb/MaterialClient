using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.AttendedWeighing;
using MaterialClient.Common.Services.Urban;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Urban.Services;

[Dependency(ReplaceServices = true)]
public class UrbanWeighingRecordSideEffects : IUrbanWeighingRecordSideEffects, ITransientDependency
{
    private readonly IUrbanWeighingExtensionService _urbanWeighingExtensionService;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;
    private readonly IRepository<WeighingRecordAttachment, int> _weighingRecordAttachmentRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ISettingsService _settingsService;
    private readonly IPlateNumberService _plateNumberService;
    private readonly ILocalEventBus _localEventBus;
    private readonly IUrbanAnomalyDetector _anomalyDetector;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UrbanWeighingRecordSideEffects> _logger;

    public UrbanWeighingRecordSideEffects(
        IUrbanWeighingExtensionService urbanWeighingExtensionService,
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IRepository<AttachmentFile, int> attachmentFileRepository,
        IRepository<WeighingRecordAttachment, int> weighingRecordAttachmentRepository,
        IUnitOfWorkManager unitOfWorkManager,
        ISettingsService settingsService,
        IPlateNumberService plateNumberService,
        ILocalEventBus localEventBus,
        IUrbanAnomalyDetector anomalyDetector,
        IConfiguration configuration,
        ILogger<UrbanWeighingRecordSideEffects> logger)
    {
        _urbanWeighingExtensionService = urbanWeighingExtensionService;
        _weighingRecordRepository = weighingRecordRepository;
        _attachmentFileRepository = attachmentFileRepository;
        _weighingRecordAttachmentRepository = weighingRecordAttachmentRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _settingsService = settingsService;
        _plateNumberService = plateNumberService;
        _localEventBus = localEventBus;
        _anomalyDetector = anomalyDetector;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task AfterWeighingRecordCreatedAsync(long weighingRecordId)
    {
        var urbanInOut = await TryResolveScaleUrbanInOutAsync();
        await _urbanWeighingExtensionService.CreateForRecordAsync(
            weighingRecordId,
            hasLprAttachment: true,
            evaluateAnomaly: false,
            urbanInOutType: urbanInOut);
    }

    private async Task<UrbanInOutType?> TryResolveScaleUrbanInOutAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        var scaleConfigs = settings.LicensePlateRecognitionConfigs?
            .Where(c => c.SiteType == LprSiteType.Scale)
            .ToList() ?? [];

        if (scaleConfigs.Count == 0)
            return null;

        return scaleConfigs[0].UrbanInOutType;
    }

    public async Task RecalculateAnomalyAfterLprOrCycleAsync(long weighingRecordId) =>
        await RecalculateAnomalyCoreAsync(weighingRecordId, promoteWeighingInProgress: false);

    public async Task FinalizeWeighingCycleAsync(long weighingRecordId) =>
        await RecalculateAnomalyCoreAsync(weighingRecordId, promoteWeighingInProgress: true);

    public async Task EnsureReadyForUploadAsync(long weighingRecordId) =>
        await RecalculateAnomalyCoreAsync(weighingRecordId, promoteWeighingInProgress: true);

    private async Task RecalculateAnomalyCoreAsync(long weighingRecordId, bool promoteWeighingInProgress)
    {
        var extension = await _urbanWeighingExtensionService.GetByWeighingRecordIdAsync(weighingRecordId);
        if (extension is null)
            return;

        var previousIsAnomaly = extension.IsAnomaly;
        var previousStatus = extension.SyncStatus;

        var record = await _weighingRecordRepository.GetAsync(weighingRecordId);

        var plateNumber = _plateNumberService.GetMostFrequentPlateNumber();
        if (!string.IsNullOrWhiteSpace(plateNumber) && record.PlateNumber != plateNumber)
        {
            using var uow = _unitOfWorkManager.Begin();
            var oldPlate = record.PlateNumber;
            record.PlateNumber = plateNumber;
            await _weighingRecordRepository.UpdateAsync(record, true);
            await uow.CompleteAsync();
            _logger.LogInformation(
                "Synced plate before Urban anomaly recalc for record {Id}: '{OldPlate}' -> '{NewPlate}'",
                weighingRecordId, oldPlate ?? "None", plateNumber);
        }

        var anomalyConfig = await UrbanAnomalyDetectionConfigLoader.LoadAsync(
            _settingsService, _configuration, _logger);
        var hasLpr = await HasLprAttachmentAsync(weighingRecordId);
        var isAnomaly = _anomalyDetector.IsAnomaly(record, anomalyConfig, hasLpr);
        var reason = isAnomaly ? _anomalyDetector.GetAnomalyReason(record, anomalyConfig, hasLpr) : null;
        await _urbanWeighingExtensionService.UpdateAnomalyStateAsync(extension.Id, isAnomaly, reason);

        if (promoteWeighingInProgress && previousStatus == SyncStatus.WeighingInProgress)
        {
            await _urbanWeighingExtensionService.UpdateSyncStatusAsync(extension.Id, SyncStatus.Pending);
            _logger.LogInformation(
                "Promoted Urban extension for record {Id} from WeighingInProgress to Pending",
                weighingRecordId);
        }
        else if (previousStatus is SyncStatus.Synced or SyncStatus.Failed &&
                 previousIsAnomaly != isAnomaly)
        {
            await _urbanWeighingExtensionService.UpdateSyncStatusAsync(extension.Id, SyncStatus.Pending);
            _logger.LogInformation(
                "Re-queued Urban extension for record {Id} to Pending after anomaly change ({Old} -> {New})",
                weighingRecordId, previousIsAnomaly, isAnomaly);
        }

        _ = _localEventBus.PublishAsync(new UpdatePlateNumberEventData(weighingRecordId, record.PlateNumber));
    }

    public async Task AfterWeighingRecordEditedAsync(long weighingRecordId, string plateNumber, decimal totalWeight)
    {
        var extension = await _urbanWeighingExtensionService.GetByWeighingRecordIdAsync(weighingRecordId);
        if (extension == null)
            return;

        await _urbanWeighingExtensionService.UpdateSyncStatusAsync(extension.Id, SyncStatus.Pending);

        var record = await _weighingRecordRepository.GetAsync(weighingRecordId);
        var anomalyConfig = await UrbanAnomalyDetectionConfigLoader.LoadAsync(
            _settingsService, _configuration, _logger);
        var hasLpr = await HasLprAttachmentAsync(weighingRecordId);
        var isAnomaly = _anomalyDetector.IsAnomaly(record, anomalyConfig, hasLpr);
        var reason = isAnomaly ? _anomalyDetector.GetAnomalyReason(record, anomalyConfig, hasLpr) : null;
        await _urbanWeighingExtensionService.UpdateAnomalyStateAsync(extension.Id, isAnomaly, reason);
    }

    private async Task<bool> HasLprAttachmentAsync(long weighingRecordId)
    {
        var recordAttachments =
            await _weighingRecordAttachmentRepository.GetListAsync(a => a.WeighingRecordId == weighingRecordId);
        if (recordAttachments.Count == 0) return false;

        var fileIds = recordAttachments.Select(a => a.AttachmentFileId).ToList();
        var lprFiles = await _attachmentFileRepository.GetListAsync(f =>
            fileIds.Contains(f.Id) && f.AttachType == AttachType.Lpr);
        return lprFiles.Count > 0;
    }
}
