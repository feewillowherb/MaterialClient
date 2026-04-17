using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

/// <summary>
///     上行同步摘要
/// </summary>
public record UploadSyncSummary(
    int AppliedCount,
    int ConflictCount,
    int FailedCount,
    int SkippedCount
);

/// <summary>
///     上行同步服务接口
/// </summary>
public interface IUploadSyncService
{
    /// <summary>
    ///     上传所有待同步的物料和供应商
    /// </summary>
    Task<UploadSyncSummary> UploadAllPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     仅上传待同步的物料
    /// </summary>
    Task<UploadSyncSummary> UploadPendingMaterialsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     仅上传待同步的供应商
    /// </summary>
    Task<UploadSyncSummary> UploadPendingProvidersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     上行同步服务实现
/// </summary>
[AutoConstructor]
public partial class UploadSyncService : DomainService, IUploadSyncService, ISingletonDependency
{
    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<Provider, int> _providerRepository;
    private readonly IRepository<SyncState, int> _syncStateRepository;
    private readonly IMaterialPlatformApi _materialPlatformApi;
    private readonly ILogger<UploadSyncService> _logger;

    private const int BatchSize = 100;
    private const int CleanupDays = 30;

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<UploadSyncSummary> UploadAllPendingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始上行同步：所有待同步项");

            // 自动清理：删除超过 30 天的 Applied 条目
            await CleanupOldSyncStatesAsync(cancellationToken);

            // 先上传物料
            var materialSummary = await UploadPendingMaterialsAsync(cancellationToken);

            // 再上传供应商
            var providerSummary = await UploadPendingProvidersAsync(cancellationToken);

            var totalSummary = new UploadSyncSummary(
                AppliedCount: materialSummary.AppliedCount + providerSummary.AppliedCount,
                ConflictCount: materialSummary.ConflictCount + providerSummary.ConflictCount,
                FailedCount: materialSummary.FailedCount + providerSummary.FailedCount,
                SkippedCount: materialSummary.SkippedCount + providerSummary.SkippedCount
            );

            _logger.LogInformation(
                "上行同步完成：已应用 {Applied}，冲突 {Conflict}，失败 {Failed}，跳过 {Skipped}",
                totalSummary.AppliedCount, totalSummary.ConflictCount, totalSummary.FailedCount, totalSummary.SkippedCount);

            return totalSummary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上行同步时发生异常");
            throw;
        }
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<UploadSyncSummary> UploadPendingMaterialsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始上行同步：物料");

            var queryable = await _syncStateRepository.GetQueryableAsync();
            var pendingSyncStates = await queryable
                .Where(s => s.Status == SyncStatus.Pending && s.EntityType == SyncEntityType.Material)
                .OrderBy(s => s.CreatedAt)
                .ToListAsync(cancellationToken);

            if (pendingSyncStates.Count == 0)
            {
                _logger.LogInformation("没有待同步的物料");
                return new UploadSyncSummary(0, 0, 0, 0);
            }

            _logger.LogInformation("找到 {Count} 条待同步物料", pendingSyncStates.Count);

            var appliedCount = 0;
            var conflictCount = 0;
            var failedCount = 0;
            var syncedEntityIds = new List<int>();

            // 分批处理
            foreach (var batch in pendingSyncStates.Chunk(BatchSize))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("物料上传已取消");
                    break;
                }

                try
                {
                    // 构建批量请求
                    var items = new List<UpsertMaterialGoodDto>();
                    var syncStateMap = new Dictionary<Guid, SyncState>();

                    foreach (var syncState in batch)
                    {
                        var material = await _materialRepository.FirstOrDefaultAsync(m => m.Id == syncState.EntityId, cancellationToken);
                        if (material == null)
                        {
                            _logger.LogWarning("物料 ID {MaterialId} 不存在，跳过", syncState.EntityId);
                            failedCount++;
                            continue;
                        }

                        var dto = UpsertMaterialGoodDto.FromEntity(
                            material,
                            syncState.LocalVersion,
                            syncState.ClientRequestId
                        );
                        items.Add(dto);
                        syncStateMap[dto.ClientRequestId] = syncState;
                    }

                    if (items.Count == 0)
                    {
                        continue;
                    }

                    // 调用批量 API
                    var request = new UpsertBatchRequestDto<UpsertMaterialGoodDto> { Items = items };
                    var results = await _materialPlatformApi.UpsertMaterialGoodsBatchAsync(request, cancellationToken);

                    // 处理结果
                    foreach (var result in results)
                    {
                        var clientRequestId = items.FirstOrDefault(i => i.GoodsId == result.EntityId)?.ClientRequestId;
                        if (clientRequestId == null || !syncStateMap.TryGetValue(clientRequestId.Value, out var syncState))
                        {
                            _logger.LogWarning("无法找到对应的 SyncState: EntityId={EntityId}", result.EntityId);
                            continue;
                        }

                        syncState.RecordAttempt();

                        switch (result.Status)
                        {
                            case "applied":
                                syncState.MarkAsApplied(result.Version ?? 0);
                                await _syncStateRepository.UpdateAsync(syncState, autoSave: true);
                                appliedCount++;
                                if (result.EntityId.HasValue)
                                {
                                    syncedEntityIds.Add(result.EntityId.Value);
                                }
                                break;

                            case "conflict":
                                syncState.MarkAsConflict(result.ServerVersion ?? 0);
                                await _syncStateRepository.UpdateAsync(syncState, autoSave: true);
                                conflictCount++;
                                // TODO: 将服务端数据应用到本地实体（任务 3.4）
                                break;

                            case "invalid":
                                _logger.LogWarning(
                                    "物料 {EntityId} 验证失败：{Errors}",
                                    result.EntityId,
                                    string.Join(", ", result.ValidationErrors ?? Enumerable.Empty<string>()));
                                failedCount++;
                                break;

                            default:
                                _logger.LogWarning("物料 {EntityId} 未知状态：{Status}", result.EntityId, result.Status);
                                failedCount++;
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理物料批次时发生异常");
                    // 批次失败：记录所有条目的尝试
                    foreach (var syncState in batch)
                    {
                        syncState.RecordAttempt();
                        await _syncStateRepository.UpdateAsync(syncState, autoSave: true);
                    }
                    failedCount += batch.Length;
                }
            }

            // 发布消息
            if (syncedEntityIds.Count > 0)
            {
                MessageBus.Current.SendMessage(new MaterialSyncedMessage(syncedEntityIds));
                _logger.LogInformation("已发布 MaterialSyncedMessage，包含 {Count} 个实体ID", syncedEntityIds.Count);
            }

            return new UploadSyncSummary(appliedCount, conflictCount, failedCount, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "物料上传时发生异常");
            throw;
        }
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<UploadSyncSummary> UploadPendingProvidersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始上行同步：供应商");

            var queryable = await _syncStateRepository.GetQueryableAsync();
            var pendingSyncStates = await queryable
                .Where(s => s.Status == SyncStatus.Pending && s.EntityType == SyncEntityType.Provider)
                .OrderBy(s => s.CreatedAt)
                .ToListAsync(cancellationToken);

            if (pendingSyncStates.Count == 0)
            {
                _logger.LogInformation("没有待同步的供应商");
                return new UploadSyncSummary(0, 0, 0, 0);
            }

            _logger.LogInformation("找到 {Count} 条待同步供应商", pendingSyncStates.Count);

            var appliedCount = 0;
            var conflictCount = 0;
            var failedCount = 0;
            var syncedEntityIds = new List<int>();

            // 分批处理
            foreach (var batch in pendingSyncStates.Chunk(BatchSize))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("供应商上传已取消");
                    break;
                }

                try
                {
                    // 构建批量请求
                    var items = new List<UpsertMaterialProviderDto>();
                    var syncStateMap = new Dictionary<Guid, SyncState>();

                    foreach (var syncState in batch)
                    {
                        var provider = await _providerRepository.FirstOrDefaultAsync(p => p.Id == syncState.EntityId, cancellationToken);
                        if (provider == null)
                        {
                            _logger.LogWarning("供应商 ID {ProviderId} 不存在，跳过", syncState.EntityId);
                            failedCount++;
                            continue;
                        }

                        var dto = UpsertMaterialProviderDto.FromEntity(
                            provider,
                            syncState.LocalVersion,
                            syncState.ClientRequestId
                        );
                        items.Add(dto);
                        syncStateMap[dto.ClientRequestId] = syncState;
                    }

                    if (items.Count == 0)
                    {
                        continue;
                    }

                    // 调用批量 API
                    var request = new UpsertBatchRequestDto<UpsertMaterialProviderDto> { Items = items };
                    var results = await _materialPlatformApi.UpsertMaterialProviderBatchAsync(request, cancellationToken);

                    // 处理结果
                    foreach (var result in results)
                    {
                        var clientRequestId = items.FirstOrDefault(i => i.ProviderId == result.EntityId)?.ClientRequestId;
                        if (clientRequestId == null || !syncStateMap.TryGetValue(clientRequestId.Value, out var syncState))
                        {
                            _logger.LogWarning("无法找到对应的 SyncState: EntityId={EntityId}", result.EntityId);
                            continue;
                        }

                        syncState.RecordAttempt();

                        switch (result.Status)
                        {
                            case "applied":
                                syncState.MarkAsApplied(result.Version ?? 0);
                                await _syncStateRepository.UpdateAsync(syncState, autoSave: true);
                                appliedCount++;
                                if (result.EntityId.HasValue)
                                {
                                    syncedEntityIds.Add(result.EntityId.Value);
                                }
                                break;

                            case "conflict":
                                syncState.MarkAsConflict(result.ServerVersion ?? 0);
                                await _syncStateRepository.UpdateAsync(syncState, autoSave: true);
                                conflictCount++;
                                // TODO: 将服务端数据应用到本地实体（任务 3.4）
                                break;

                            case "invalid":
                                _logger.LogWarning(
                                    "供应商 {EntityId} 验证失败：{Errors}",
                                    result.EntityId,
                                    string.Join(", ", result.ValidationErrors ?? Enumerable.Empty<string>()));
                                failedCount++;
                                break;

                            default:
                                _logger.LogWarning("供应商 {EntityId} 未知状态：{Status}", result.EntityId, result.Status);
                                failedCount++;
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理供应商批次时发生异常");
                    // 批次失败：记录所有条目的尝试
                    foreach (var syncState in batch)
                    {
                        syncState.RecordAttempt();
                        await _syncStateRepository.UpdateAsync(syncState, autoSave: true);
                    }
                    failedCount += batch.Length;
                }
            }

            // 发布消息
            if (syncedEntityIds.Count > 0)
            {
                MessageBus.Current.SendMessage(new ProviderSyncedMessage(syncedEntityIds));
                _logger.LogInformation("已发布 ProviderSyncedMessage，包含 {Count} 个实体ID", syncedEntityIds.Count);
            }

            return new UploadSyncSummary(appliedCount, conflictCount, failedCount, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "供应商上传时发生异常");
            throw;
        }
    }

    /// <summary>
    ///     自动清理：删除超过 30 天的 Applied SyncState 条目
    /// </summary>
    [UnitOfWork]
    private async Task CleanupOldSyncStatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-CleanupDays);

            var queryable = await _syncStateRepository.GetQueryableAsync();
            var oldSyncStates = await queryable
                .Where(s => s.Status == SyncStatus.Applied && s.UpdatedAt < cutoffDate)
                .ToListAsync(cancellationToken);

            if (oldSyncStates.Count > 0)
            {
                await _syncStateRepository.DeleteManyAsync(oldSyncStates, autoSave: true);
                _logger.LogInformation("清理了 {Count} 条过期 SyncState 条目（超过 {Days} 天）",
                    oldSyncStates.Count, CleanupDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理 SyncState 时发生异常");
            // 不抛出异常，清理失败不应阻塞同步
        }
    }
}
