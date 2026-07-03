using MaterialClient.Common.Dtos.Urban;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services.Urban;

/// <inheritdoc />
public class UrbanWeighingExtensionService : DomainService, IUrbanWeighingExtensionService
{
    private readonly IRepository<UrbanWeighingExtension, Guid> _extensionRepository;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IUrbanAnomalyDetector _anomalyDetector;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UrbanWeighingExtensionService> _logger;

    public UrbanWeighingExtensionService(
        IRepository<UrbanWeighingExtension, Guid> extensionRepository,
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IUrbanAnomalyDetector anomalyDetector,
        IConfiguration configuration,
        ILogger<UrbanWeighingExtensionService> logger)
    {
        _extensionRepository = extensionRepository;
        _weighingRecordRepository = weighingRecordRepository;
        _anomalyDetector = anomalyDetector;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public virtual async Task<UrbanWeighingExtension> CreateForRecordAsync(long weighingRecordId, bool hasLprAttachment = true)
    {
        if (weighingRecordId <= 0)
        {
            throw new BusinessException("UrbanWeighingExtension: WeighingRecordId must be greater than zero.");
        }

        var existing = await _extensionRepository.FirstOrDefaultAsync(e => e.WeighingRecordId == weighingRecordId);
        if (existing != null)
        {
            throw new BusinessException(
                $"UrbanWeighingExtension already exists for WeighingRecordId {weighingRecordId}.");
        }

        var extension = new UrbanWeighingExtension
        {
            WeighingRecordId = weighingRecordId,
            SyncStatus = SyncStatus.Pending,
            RetryCount = 0,
            LastErrorTime = null
        };

        // Persist AnomalyReason at creation time (includes Lrp absence check)
        var record = await _weighingRecordRepository.GetAsync(weighingRecordId);
        var anomalyConfig = GetAnomalyDetectionConfig();
        extension.IsAnomaly = _anomalyDetector.IsAnomaly(record, anomalyConfig, hasLprAttachment);
        extension.AnomalyReason = extension.IsAnomaly
            ? _anomalyDetector.GetAnomalyReason(record, anomalyConfig, hasLprAttachment)
            : null;

        await _extensionRepository.InsertAsync(extension, autoSave: true);
        _logger.LogDebug("Created UrbanWeighingExtension {ExtensionId} for WeighingRecord {RecordId}",
            extension.Id, weighingRecordId);

        return extension;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public virtual async Task<UrbanWeighingExtension?> GetByIdAsync(Guid extensionId)
    {
        if (extensionId == Guid.Empty)
        {
            return null;
        }

        return await _extensionRepository.FindAsync(extensionId);
    }

    /// <inheritdoc />
    [UnitOfWork]
    public virtual async Task<UrbanWeighingExtension?> GetByWeighingRecordIdAsync(long weighingRecordId)
    {
        if (weighingRecordId <= 0)
        {
            return null;
        }

        return await _extensionRepository.FirstOrDefaultAsync(e => e.WeighingRecordId == weighingRecordId);
    }

    /// <inheritdoc />
    [UnitOfWork]
    public virtual async Task<PagedResultDto<UrbanWeighingListItemDto>> GetPagedListItemsAsync(
        GetUrbanWeighingListInput input)
    {
        var pageIndex = input.PageIndex < 1 ? 1 : input.PageIndex;
        var pageSize = input.PageSize < 1 ? 20 : input.PageSize;

        var recordQueryable = await _weighingRecordRepository.GetQueryableAsync();
        var extensionQueryable = await _extensionRepository.GetQueryableAsync();

        var joined = from r in recordQueryable
            where r.WeighingMode == WeighingMode.UrbanMode
            join e in extensionQueryable on r.Id equals e.WeighingRecordId into extGroup
            from e in extGroup.DefaultIfEmpty()
            select new { Record = r, Extension = e };

        joined = input.TabFilter switch
        {
            // 与列表 DTO 一致：无扩展行时 IsAnomaly 视为 false，归入「正常」
            "正常" => joined.Where(x =>
                x.Extension == null || !x.Extension.IsAnomaly),
            "异常" => joined.Where(x =>
                x.Extension != null && x.Extension.IsAnomaly),
            _ => joined
        };

        if (!string.IsNullOrWhiteSpace(input.SearchText))
        {
            joined = joined.Where(x =>
                x.Record.PlateNumber != null && x.Record.PlateNumber.Contains(input.SearchText));
        }

        if (input.StartTime.HasValue)
        {
            joined = joined.Where(x => x.Record.AddDate >= input.StartTime.Value);
        }

        if (input.EndTime.HasValue)
        {
            joined = joined.Where(x => x.Record.AddDate <= input.EndTime.Value);
        }

        var totalCount = await joined.CountAsync();
        var rows = await joined
            .OrderByDescending(x => x.Record.AddDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = rows.Select(x => new UrbanWeighingListItemDto
        {
            WeighingRecordId = x.Record.Id,
            PlateNumber = x.Record.PlateNumber,
            AddDate = x.Record.AddDate,
            TotalWeight = x.Record.TotalWeight,
            IsAnomaly = x.Extension?.IsAnomaly ?? false,
            SyncStatus = x.Extension?.SyncStatus,
            AnomalyReason = x.Extension?.AnomalyReason,
            UploadTime = x.Extension?.SyncStatus == SyncStatus.Synced ? x.Record.UpdateDate ?? x.Record.AddDate : null
        }).ToList();

        return new PagedResultDto<UrbanWeighingListItemDto>(totalCount, items);
    }

    /// <inheritdoc />
    public virtual async Task<List<UrbanWeighingExtension>> GetPendingForUploadAsync(int maxCount = 100)
    {
        var queryable = await _extensionRepository.GetQueryableAsync();
        return await queryable
            .Where(e => e.SyncStatus == SyncStatus.Pending)
            .OrderBy(e => e.WeighingRecordId)
            .Take(maxCount)
            .ToListAsync();
    }

    /// <inheritdoc />
    [UnitOfWork]
    public virtual async Task UpdateSyncStatusAsync(
        Guid extensionId,
        SyncStatus syncStatus,
        DateTime? lastErrorTime = null)
    {
        var extension = await _extensionRepository.GetAsync(extensionId);
        extension.SyncStatus = syncStatus;

        if (syncStatus == SyncStatus.Failed)
        {
            extension.RetryCount++;
            extension.LastErrorTime = lastErrorTime ?? DateTime.UtcNow;
        }
        else if (syncStatus == SyncStatus.Synced)
        {
            extension.LastErrorTime = null;
        }

        await _extensionRepository.UpdateAsync(extension, autoSave: true);
    }

    /// <inheritdoc />
    [UnitOfWork]
    public virtual async Task UpdateAnomalyStateAsync(
        Guid extensionId,
        bool isAnomaly,
        AnomalyReason? anomalyReason)
    {
        var extension = await _extensionRepository.GetAsync(extensionId);
        extension.IsAnomaly = isAnomaly;
        extension.AnomalyReason = anomalyReason;
        await _extensionRepository.UpdateAsync(extension, autoSave: true);
    }

    /// <inheritdoc />
    [UnitOfWork]
    public virtual async Task AppendEditEntryAsync(
        Guid extensionId,
        EditEntrySnapshot before,
        EditEntrySnapshot after,
        EditSource source = EditSource.Client,
        bool isImagesModified = false)
    {
        var extension = await _extensionRepository.GetAsync(extensionId);
        var history = extension.GetEditHistory();
        history.Add(new EditEntry
        {
            ChangedAt = DateTime.Now,
            Before = before,
            After = after,
            Source = source,
            IsImagesModified = isImagesModified
        });
        extension.SetEditHistory(history);
        await _extensionRepository.UpdateAsync(extension, autoSave: true);
    }

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
}