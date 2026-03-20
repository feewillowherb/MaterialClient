using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Application.Dtos;

namespace MaterialClient.Common.Services;

public interface ISolidWasteService
{
    Task<IReadOnlyList<SolidWasteExportRow>> GetExportRowsAsync(SolidWasteExportFilter filter);

    /// <summary>
    ///     分页查询固废运单导出行，用于数据管理对话框按页展示。
    /// </summary>
    Task<PagedResultDto<SolidWasteExportRow>> GetPagedExportRowsAsync(SolidWasteExportFilter filter, int pageIndex, int pageSize);
}

[AutoConstructor]
public partial class SolidWasteService : ISolidWasteService, ITransientDependency
{
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IRepository<Provider, int> _providerRepository;
    private readonly IRepository<Material, int> _materialRepository;

    [UnitOfWork]
    public virtual async Task<IReadOnlyList<SolidWasteExportRow>> GetExportRowsAsync(SolidWasteExportFilter filter)
    {
        var waybills = await QueryWaybillsAsync(filter);
        var providerDict = await BuildProviderDictAsync(waybills);
        var materialDict = await BuildMaterialDictAsync(waybills);
        return waybills
            .Select(w => MapToExportRow(w, providerDict, materialDict))
            .ToList();
    }

    [UnitOfWork]
    public virtual async Task<PagedResultDto<SolidWasteExportRow>> GetPagedExportRowsAsync(
        SolidWasteExportFilter filter,
        int pageIndex,
        int pageSize)
    {
        var waybills = await QueryWaybillsAsync(filter);
        var totalCount = waybills.Count;
        var page = waybills
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        var providerDict = await BuildProviderDictAsync(page);
        var materialDict = await BuildMaterialDictAsync(page);
        var items = page
            .Select(w => MapToExportRow(w, providerDict, materialDict))
            .ToList();
        return new PagedResultDto<SolidWasteExportRow>(totalCount, items);
    }

    private async Task<List<Waybill>> QueryWaybillsAsync(SolidWasteExportFilter filter)
    {
        var queryable = await _waybillRepository.GetQueryableAsync();

        queryable = queryable.Where(w =>
            w.WeighingMode == WeighingMode.SolidWaste &&
            w.OrderType == OrderTypeEnum.Completed);

        if (filter.StartDate.HasValue)
            queryable = queryable.Where(w => w.AddDate >= filter.StartDate.Value);

        if (filter.EndDate.HasValue)
            queryable = queryable.Where(w => w.AddDate <= filter.EndDate.Value);

        if (!string.IsNullOrWhiteSpace(filter.PlateNumber))
            queryable = queryable.Where(w =>
                w.PlateNumber != null && w.PlateNumber.Contains(filter.PlateNumber));

        if (filter.WeighingType.HasValue)
            queryable = queryable.Where(w => w.DeliveryType == filter.WeighingType.Value);

        var waybills = await queryable.OrderBy(w => w.AddDate).ToListAsync();

        if (!string.IsNullOrWhiteSpace(filter.ProviderName))
        {
            var providerIds = (await _providerRepository.GetQueryableAsync())
                .Where(p => p.ProviderName.Contains(filter.ProviderName))
                .Select(p => (int?)p.Id);
            waybills = waybills
                .Where(w => w.ProviderId.HasValue && providerIds.Contains(w.ProviderId))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(filter.GoodsName))
        {
            var matchedMaterialIds = (await _materialRepository.GetQueryableAsync())
                .Where(m => m.Name.Contains(filter.GoodsName))
                .Select(m => m.Id)
                .ToHashSet();

            waybills = waybills
                .Where(w =>
                {
                    var mid = w.GetProperty<int?>("SolidWasteInfo.MaterialId");
                    return mid.HasValue && matchedMaterialIds.Contains(mid.Value);
                })
                .ToList();
        }

        return waybills;
    }

    private async Task<Dictionary<int, string>> BuildProviderDictAsync(List<Waybill> waybills)
    {
        var providerIds = waybills
            .Where(w => w.ProviderId.HasValue)
            .Select(w => w.ProviderId!.Value)
            .Distinct()
            .ToList();

        if (providerIds.Count == 0) return new Dictionary<int, string>();

        return (await _providerRepository.GetQueryableAsync())
            .Where(p => providerIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p.ProviderName);
    }

    private async Task<Dictionary<int, string>> BuildMaterialDictAsync(List<Waybill> waybills)
    {
        var materialIds = waybills
            .Select(w => w.GetProperty<int?>("SolidWasteInfo.MaterialId"))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (materialIds.Count == 0) return new Dictionary<int, string>();

        return (await _materialRepository.GetQueryableAsync())
            .Where(m => materialIds.Contains(m.Id))
            .ToDictionary(m => m.Id, m => m.Name);
    }

    internal static SolidWasteExportRow MapToExportRow(
        Waybill waybill,
        Dictionary<int, string> providerDict,
        Dictionary<int, string> materialDict)
    {
        var providerName = waybill.ProviderId.HasValue &&
                           providerDict.TryGetValue(waybill.ProviderId.Value, out var pn)
            ? pn
            : string.Empty;

        var materialId = waybill.GetProperty<int?>("SolidWasteInfo.MaterialId");
        var goodsName = materialId.HasValue &&
                        materialDict.TryGetValue(materialId.Value, out var mn)
            ? mn
            : string.Empty;

        var weighingType = waybill.DeliveryType switch
        {
            DeliveryType.Receiving => "收料",
            DeliveryType.Sending => "发料",
            _ => string.Empty
        };

        return new SolidWasteExportRow
        {
            SerialNumber = waybill.OrderNo ?? string.Empty,
            VehicleNumber = waybill.PlateNumber ?? string.Empty,
            WeighingType = weighingType,
            ShippingUnit = providerName,
            ReceivingUnit = waybill.GetShipper(),
            GoodsName = goodsName,
            GrossWeight = waybill.OrderTotalWeight,
            TareWeight = waybill.OrderTruckWeight,
            NetWeight = waybill.OrderGoodsWeight,
            Remark = waybill.Remark ?? string.Empty,
            GrossWeightTime = waybill.JoinTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
            TareWeightTime = waybill.OutTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
            Street = waybill.GetStreet() ?? string.Empty,
            SolidWasteType = waybill.GetSolidWasteType() ?? string.Empty,
            ManifestNumber = waybill.GetSolidWasteOrderNumber() ?? string.Empty,
            UploadResult = waybill.IsPendingSync ? "0" : "1",
            UploadStatus = waybill.IsPendingSync ? "未上传" : "上传成功",
            UploadTime = waybill.LastSyncTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty
        };
    }
}
