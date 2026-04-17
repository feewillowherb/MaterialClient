using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

/// <summary>
///     供应商服务接口
/// </summary>
public interface IProviderService
{
    /// <summary>
    ///     获取所有供应商列表（未删除的）
    /// </summary>
    /// <returns>供应商列表，按供应商名称排序</returns>
    Task<List<Provider>> GetAllProvidersAsync();

    /// <summary>
    ///     分页查询供应商列表
    /// </summary>
    /// <param name="searchText">搜索关键字（可选）</param>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="selectedIds">已选 id 列表，保证这些项出现在当前页结果前部</param>
    /// <returns>分页结果，Items 条数为 pageSize + selectedIds.Count（或更少）</returns>
    Task<PagedResultDto<ProviderDto>> GetPagedProvidersAsync(
        string? searchText = null,
        int pageIndex = 1,
        int pageSize = 10,
        IReadOnlyList<int>? selectedIds = null);

    /// <summary>
    ///     新增供应商
    /// </summary>
    /// <param name="providerName">供应商名称</param>
    /// <param name="deliveryType">当前称重记录/联单的 DeliveryType</param>
    Task<Provider> CreateProviderAsync(string providerName, DeliveryType deliveryType);

    /// <summary>
    ///     更新供应商信息
    /// </summary>
    Task<ProviderDto> UpdateProviderAsync(int id, string providerName, string? contactName, string? contactPhone);
}

/// <summary>
///     供应商服务实现
/// </summary>
[AutoConstructor]
public partial class ProviderService : DomainService, IProviderService
{
    private readonly IRepository<Provider, int> _providerRepository;
    private readonly IRepository<SyncState, int> _syncStateRepository;
    private readonly ISettingsService _settingsService;

    /// <inheritdoc />
    [UnitOfWork]
    public virtual async Task<List<Provider>> GetAllProvidersAsync()
    {
        var weighingMode = await _settingsService.GetWeighingModeAsync();

        var queryable = await _providerRepository.GetQueryableAsync();
        queryable = queryable.AsNoTracking();

        queryable = queryable.Where(p => !p.IsDeleted);
        queryable = queryable.Where(p => p.WeighingMode == weighingMode);

        var providers = await queryable
            .OrderBy(p => p.ProviderName)
            .ToListAsync();

        return providers;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public virtual async Task<PagedResultDto<ProviderDto>> GetPagedProvidersAsync(
        string? searchText = null,
        int pageIndex = 1,
        int pageSize = 10,
        IReadOnlyList<int>? selectedIds = null)
    {
        var weighingMode = await _settingsService.GetWeighingModeAsync();

        var queryable = await _providerRepository.GetQueryableAsync();
        queryable = queryable.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.Trim();
            queryable = queryable.Where(p => p.ProviderName != null && p.ProviderName.Contains(search));
        }

        queryable = queryable.Where(p => !p.IsDeleted);
        queryable = queryable.Where(p => p.WeighingMode == weighingMode);

        var totalCount = await queryable.CountAsync();

        var merged = new List<ProviderDto>();

        if (selectedIds != null && selectedIds.Count > 0)
        {
            var selectedList = await queryable
                .Where(p => selectedIds.Contains(p.Id))
                .OrderBy(p => p.ProviderName)
                .Select(p => new ProviderDto
                {
                    Id = p.Id,
                    ProviderType = p.ProviderType ?? 0,
                    ProviderName = p.ProviderName ?? string.Empty,
                    ContactName = p.ContectName,
                    ContactPhone = p.ContectPhone
                })
                .ToListAsync();
            var selectedSet = selectedList.Select(p => p.Id).ToHashSet();
            merged.AddRange(selectedList);

            var pageItems = await queryable
                .Where(p => !selectedSet.Contains(p.Id))
                .OrderBy(p => p.ProviderName)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProviderDto
                {
                    Id = p.Id,
                    ProviderType = p.ProviderType ?? 0,
                    ProviderName = p.ProviderName ?? string.Empty,
                    ContactName = p.ContectName,
                    ContactPhone = p.ContectPhone
                })
                .ToListAsync();
            merged.AddRange(pageItems);
        }
        else
        {
            var items = await queryable
                .OrderBy(p => p.ProviderName)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProviderDto
                {
                    Id = p.Id,
                    ProviderType = p.ProviderType ?? 0,
                    ProviderName = p.ProviderName ?? string.Empty,
                    ContactName = p.ContectName,
                    ContactPhone = p.ContectPhone
                })
                .ToListAsync();
            merged.AddRange(items);
        }

        return new PagedResultDto<ProviderDto>(totalCount, merged);
    }

    /// <inheritdoc />
    [UnitOfWork]
    public virtual async Task<Provider> CreateProviderAsync(string providerName, DeliveryType deliveryType)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name is required.", nameof(providerName));
        }

        var weighingMode = await _settingsService.GetWeighingModeAsync();
        var now = DateTime.Now;

        var provider = new Provider(
            providerType: (int)deliveryType,
            providerName: providerName.Trim())
        {
            CoId = 1, // TODO update in next version
            WeighingMode = weighingMode,
            AddDate = now,
            AddTime = (int)DateTimeOffset.Now.ToUnixTimeSeconds(),
            IsDeleted = false
        };

        provider = await _providerRepository.InsertAsync(provider, autoSave: true);

        var localVersion = provider.UpdateTime ?? provider.AddTime;
        await UpsertProviderSyncStateAsync(provider.Id, localVersion);

        return provider;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public virtual async Task<ProviderDto> UpdateProviderAsync(
        int id,
        string providerName,
        string? contactName,
        string? contactPhone)
    {
        var weighingMode = await _settingsService.GetWeighingModeAsync();

        var queryable = await _providerRepository.GetQueryableAsync();
        var provider = await queryable
            .Where(p => p.Id == id)
            .Where(p => !p.IsDeleted)
            .Where(p => p.WeighingMode == weighingMode)
            .FirstOrDefaultAsync();

        if (provider == null)
        {
            throw new ArgumentException($"Provider(id={id}) not found.", nameof(id));
        }

        provider.UpdateInfo(providerName, contactName, contactPhone);
        provider.UpdateTime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await _providerRepository.UpdateAsync(provider, autoSave: true);
        await UpsertProviderSyncStateAsync(provider.Id, provider.UpdateTime.Value);

        return new ProviderDto
        {
            Id = provider.Id,
            ProviderType = provider.ProviderType ?? 0,
            ProviderName = provider.ProviderName ?? string.Empty,
            ContactName = provider.ContectName,
            ContactPhone = provider.ContectPhone
        };
    }

    private async Task UpsertProviderSyncStateAsync(int providerId, long localVersion)
    {
        var syncState = await _syncStateRepository.FirstOrDefaultAsync(
            s => s.EntityType == SyncEntityType.Provider && s.EntityId == providerId);

        if (syncState == null)
        {
            var newSyncState = new SyncState(
                SyncEntityType.Provider,
                providerId,
                localVersion,
                Guid.NewGuid());

            await _syncStateRepository.InsertAsync(newSyncState, autoSave: true);
            return;
        }

        syncState.ResetToPending(localVersion);
        syncState.ClientRequestId = Guid.NewGuid();
        await _syncStateRepository.UpdateAsync(syncState, autoSave: true);
    }
}
