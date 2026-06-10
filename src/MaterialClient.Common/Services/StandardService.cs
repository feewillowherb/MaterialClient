using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

public interface IStandardService
{
    Task<IReadOnlyList<StandardExportRow>> GetExportRowsAsync(StandardExportFilter filter);

    /// <summary>
    ///     分页查询运单导出行，按 WeighingMode 过滤，用于数据管理对话框按页展示。
    /// </summary>
    Task<PagedResultDto<StandardExportRow>> GetPagedExportRowsAsync(StandardExportFilter filter, int pageIndex, int pageSize);
}

[AutoConstructor]
public partial class StandardService : IStandardService, ITransientDependency
{
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<Provider, int> _providerRepository;

    [UnitOfWork]
    public virtual async Task<IReadOnlyList<StandardExportRow>> GetExportRowsAsync(StandardExportFilter filter)
    {
        var queryable = await BuildFilteredQueryableAsync(filter);
        var waybills = await queryable.OrderByDescending(w => w.JoinTime).ToListAsync();
        var providerDict = await BuildProviderDictAsync(waybills);
        var materialDict = await BuildMaterialDictAsync(waybills);
        return waybills
            .Select(w => MapToExportRow(w, providerDict, materialDict))
            .ToList();
    }

    [UnitOfWork]
    public virtual async Task<PagedResultDto<StandardExportRow>> GetPagedExportRowsAsync(
        StandardExportFilter filter, int pageIndex, int pageSize)
    {
        var queryable = await BuildFilteredQueryableAsync(filter);
        var totalCount = await queryable.CountAsync();

        var pagedWaybills = await queryable
            .OrderByDescending(w => w.JoinTime)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var providerDict = await BuildProviderDictAsync(pagedWaybills);
        var materialDict = await BuildMaterialDictAsync(pagedWaybills);
        var items = pagedWaybills
            .Select(w => MapToExportRow(w, providerDict, materialDict))
            .ToList();

        return new PagedResultDto<StandardExportRow>(totalCount, items);
    }

    private async Task<IQueryable<Waybill>> BuildFilteredQueryableAsync(StandardExportFilter filter)
    {
        var queryable = await _waybillRepository.GetQueryableAsync();

        queryable = queryable.Where(w =>
            w.WeighingMode == filter.WeighingMode && !w.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.PlateNumber))
            queryable = queryable.Where(w =>
                w.PlateNumber != null && w.PlateNumber.Contains(filter.PlateNumber));

        if (filter.DeliveryType.HasValue)
            queryable = queryable.Where(w => w.DeliveryType == filter.DeliveryType.Value);

        if (filter.OrderType.HasValue)
            queryable = queryable.Where(w => w.OrderType == filter.OrderType.Value);

        if (filter.StartDate.HasValue)
            queryable = queryable.Where(w =>
                w.JoinTime != null && w.JoinTime >= filter.StartDate.Value);

        var effectiveEndDate = filter.GetEffectiveEndDate();
        if (effectiveEndDate.HasValue)
            queryable = queryable.Where(w =>
                w.JoinTime != null && w.JoinTime < effectiveEndDate.Value);

        if (!string.IsNullOrWhiteSpace(filter.MaterialName))
        {
            var matchedMaterialIds = (await _materialRepository.GetQueryableAsync())
                .Where(m => m.Name.Contains(filter.MaterialName))
                .Select(m => m.Id)
                .ToHashSet();

            queryable = queryable.Where(w =>
                w.MaterialId.HasValue && matchedMaterialIds.Contains(w.MaterialId.Value));
        }

        return queryable;
    }

    private async Task<Dictionary<int, string>> BuildProviderDictAsync(List<Waybill> waybills)
    {
        var providerIds = waybills
            .Where(w => w.ProviderId.HasValue)
            .Select(w => w.ProviderId!.Value)
            .Distinct()
            .ToList();

        if (providerIds.Count == 0) return [];

        return (await _providerRepository.GetQueryableAsync())
            .Where(p => providerIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p.ProviderName);
    }

    private async Task<Dictionary<int, string>> BuildMaterialDictAsync(List<Waybill> waybills)
    {
        var materialIds = waybills
            .Where(w => w.MaterialId.HasValue)
            .Select(w => w.MaterialId!.Value)
            .Distinct()
            .ToList();

        if (materialIds.Count == 0) return [];

        return (await _materialRepository.GetQueryableAsync())
            .Where(m => materialIds.Contains(m.Id))
            .ToDictionary(m => m.Id, m => m.Name);
    }

    private static StandardExportRow MapToExportRow(
        Waybill waybill,
        Dictionary<int, string> providerDict,
        Dictionary<int, string> materialDict)
    {
        var providerName = waybill.ProviderId.HasValue &&
                           providerDict.TryGetValue(waybill.ProviderId.Value, out var pn)
            ? pn
            : string.Empty;

        var materialName = waybill.MaterialId.HasValue &&
                           materialDict.TryGetValue(waybill.MaterialId.Value, out var mn)
            ? mn
            : string.Empty;

        return new StandardExportRow
        {
            PlateNumber = waybill.PlateNumber ?? string.Empty,
            DeliveryType = waybill.DeliveryType switch
            {
                DeliveryType.Receiving => "收料",
                DeliveryType.Sending => "发料",
                _ => string.Empty
            },
            MaterialName = materialName,
            OrderType = waybill.OrderType switch
            {
                OrderTypeEnum.FirstWeight => "首称中",
                OrderTypeEnum.Completed => "已完成",
                OrderTypeEnum.Esc => "已取消",
                _ => string.Empty
            },
            PlanQuantity = waybill.OrderPlanOnPcs,
            PlanWeight = waybill.OrderPlanOnWeight,
            OffsetCount = waybill.OffsetCount,
            ActualQuantity = waybill.OrderPcs,
            ActualWeight = waybill.OrderGoodsWeight,
            UnitConversion = waybill.MaterialUnitRate,
            JoinTime = waybill.JoinTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
            OutTime = waybill.OutTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
            ProviderName = providerName,
            OrderNo = waybill.OrderNo ?? string.Empty,
            Remark = waybill.Remark ?? string.Empty
        };
    }
}
