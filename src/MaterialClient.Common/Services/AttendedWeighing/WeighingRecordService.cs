using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using MaterialClient.Common.Events;
using MaterialClient.Common.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Dtos;
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
    ///     分页查询 Urban 称重记录，支持标签过滤、车牌号搜索、时间范围过滤
    /// </summary>
    Task<PagedResultDto<WeighingRecord>> GetPagedUrbanWeighingRecordsAsync(
        int pageIndex, int pageSize, string? tabFilter, string? searchText, DateTime? startTime, DateTime? endTime);
}

/// <summary>
///     称重记录服务
///     处理 WeighingRecord 创建、车牌号重写、照片附件保存和 TryMatch 事件发布
/// </summary>
public class WeighingRecordService : IWeighingRecordService, ISingletonDependency
{
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<UrbanWeighingExtension, Guid> _urbanWeighingExtensionRepository;
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;
    private readonly IRepository<WeighingRecordAttachment, int> _weighingRecordAttachmentRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ISettingsService _settingsService;
    private readonly IPlateNumberService _plateNumberService;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<WeighingRecordService> _logger;
    private readonly IWeighingPipelineStrategy _pipelineStrategy;

    public WeighingRecordService(
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IRepository<UrbanWeighingExtension, Guid> urbanWeighingExtensionRepository,
        IRepository<AttachmentFile, int> attachmentFileRepository,
        IRepository<WeighingRecordAttachment, int> weighingRecordAttachmentRepository,
        IUnitOfWorkManager unitOfWorkManager,
        ISettingsService settingsService,
        IPlateNumberService plateNumberService,
        ILocalEventBus localEventBus,
        ILogger<WeighingRecordService> logger,
        IWeighingPipelineStrategy? pipelineStrategy = null)
    {
        _weighingRecordRepository = weighingRecordRepository;
        _urbanWeighingExtensionRepository = urbanWeighingExtensionRepository;
        _attachmentFileRepository = attachmentFileRepository;
        _weighingRecordAttachmentRepository = weighingRecordAttachmentRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _settingsService = settingsService;
        _plateNumberService = plateNumberService;
        _localEventBus = localEventBus;
        _logger = logger;
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

            var weighingMode = await _settingsService.GetWeighingModeAsync();
            weighingRecord.SetWeighingMode(weighingMode);

            await _weighingRecordRepository.InsertAsync(weighingRecord);

            // Create UrbanWeighingExtension for Urban mode records (transactional)
            if (weighingMode == WeighingMode.UrbanMode)
            {
                var extension = new UrbanWeighingExtension
                {
                    WeighingRecordId = weighingRecord.Id,
                    SyncStatus = SyncStatus.Pending,
                    RetryCount = 0,
                    LastErrorTime = null
                };
                await _urbanWeighingExtensionRepository.InsertAsync(extension);

                _logger.LogDebug("Created UrbanWeighingExtension for record {Id}", weighingRecord.Id);
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
                    var attachmentFile = new AttachmentFile(fileName, relativePath, AttachType.UnmatchedEntryPhoto);

                    await _attachmentFileRepository.InsertAsync(attachmentFile, true);

                    var weighingRecordAttachment = new WeighingRecordAttachment(weighingRecordId, attachmentFile.Id);
                    await _weighingRecordAttachmentRepository.InsertAsync(weighingRecordAttachment, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save photo: {PhotoPath}", photoPath);
                }

            await uow.CompleteAsync();
            _logger.LogInformation("Saved {Count} photos to weighing record {Id}", photoPaths.Count,
                weighingRecordId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while saving captured photos");
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
    public virtual async Task<PagedResultDto<WeighingRecord>> GetPagedUrbanWeighingRecordsAsync(
        int pageIndex, int pageSize, string? tabFilter, string? searchText, DateTime? startTime, DateTime? endTime)
    {
        var queryable = await _weighingRecordRepository.GetQueryableAsync();

        // Filter by UrbanMode and Include UrbanExtension
        queryable = queryable
            .Include(r => r.UrbanExtension)
            .Where(r => r.WeighingMode == WeighingMode.UrbanMode);

        // Tab filter: filter by UrbanExtension.SyncStatus
        queryable = tabFilter switch
        {
            "正常" => queryable.Where(r =>
                r.UrbanExtension != null && r.UrbanExtension.SyncStatus != SyncStatus.Failed),
            "异常" => queryable.Where(r =>
                r.UrbanExtension != null && r.UrbanExtension.SyncStatus == SyncStatus.Failed),
            _ => queryable // "全部" or null
        };

        // Search text: plate number fuzzy query
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            queryable = queryable.Where(r =>
                r.PlateNumber != null && r.PlateNumber.Contains(searchText));
        }

        // Time range filter
        if (startTime.HasValue)
        {
            queryable = queryable.Where(r => r.AddDate >= startTime.Value);
        }

        if (endTime.HasValue)
        {
            queryable = queryable.Where(r => r.AddDate <= endTime.Value);
        }

        // Pagination
        var totalCount = await queryable.CountAsync();
        var records = await queryable
            .OrderByDescending(r => r.AddDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<WeighingRecord>(totalCount, records);
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
}
