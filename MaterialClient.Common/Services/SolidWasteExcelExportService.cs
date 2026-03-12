using ClosedXML.Excel;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

public interface ISolidWasteExcelExportService
{
    Task<ExportResult> ExportAsync(SolidWasteExportFilter filter, string outputPath);
}

[AutoConstructor]
public partial class SolidWasteExcelExportService : ISolidWasteExcelExportService, ITransientDependency
{
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IRepository<Provider, int> _providerRepository;
    private readonly IRepository<Material, int> _materialRepository;
    private readonly ILogger<SolidWasteExcelExportService> _logger;

    private static readonly string[] Headers =
    [
        "流水号", "车  号", "发货单位", "收货单位", "货  名",
        "毛  重", "皮  重", "净  重", "备 注", "毛重时间",
        "皮重时间", "所属街道", "类型", "联单编号",
        "上传结果", "上传状态", "上传时间"
    ];

    [UnitOfWork]
    public virtual async Task<ExportResult> ExportAsync(SolidWasteExportFilter filter, string outputPath)
    {
        try
        {
            var waybills = await QueryWaybillsAsync(filter);

            var providerDict = await BuildProviderDictAsync(waybills);
            var materialDict = await BuildMaterialDictAsync(waybills);

            var rows = waybills
                .Select(w => MapToExportRow(w, providerDict, materialDict))
                .ToList();

            WriteExcel(rows, outputPath);

            return new ExportResult
            {
                RowCount = rows.Count,
                FilePath = outputPath,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "固废运单导出失败: {OutputPath}", outputPath);
            return new ExportResult
            {
                RowCount = 0,
                FilePath = outputPath,
                Success = false
            };
        }
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

        return new SolidWasteExportRow
        {
            SerialNumber = waybill.OrderNo ?? string.Empty,
            VehicleNumber = waybill.PlateNumber ?? string.Empty,
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

    private static void WriteExcel(List<SolidWasteExportRow> rows, string outputPath)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sheet1");

        for (var i = 0; i < Headers.Length; i++)
            ws.Cell(1, i + 1).Value = Headers[i];

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var excelRow = r + 2;
            ws.Cell(excelRow, 1).Value = row.SerialNumber;
            ws.Cell(excelRow, 2).Value = row.VehicleNumber;
            ws.Cell(excelRow, 3).Value = row.ShippingUnit;
            ws.Cell(excelRow, 4).Value = row.ReceivingUnit;
            ws.Cell(excelRow, 5).Value = row.GoodsName;
            ws.Cell(excelRow, 6).Value = row.GrossWeight ?? 0;
            ws.Cell(excelRow, 7).Value = row.TareWeight ?? 0;
            ws.Cell(excelRow, 8).Value = row.NetWeight ?? 0;
            ws.Cell(excelRow, 9).Value = row.Remark;
            ws.Cell(excelRow, 10).Value = row.GrossWeightTime;
            ws.Cell(excelRow, 11).Value = row.TareWeightTime;
            ws.Cell(excelRow, 12).Value = row.Street;
            ws.Cell(excelRow, 13).Value = row.SolidWasteType;
            ws.Cell(excelRow, 14).Value = row.ManifestNumber;
            ws.Cell(excelRow, 15).Value = row.UploadResult;
            ws.Cell(excelRow, 16).Value = row.UploadStatus;
            ws.Cell(excelRow, 17).Value = row.UploadTime;
        }

        var summaryRow = rows.Count + 2;
        ws.Cell(summaryRow, 1).Value = rows.Count;
        ws.Cell(summaryRow, 6).Value = rows.Sum(r => r.GrossWeight ?? 0);
        ws.Cell(summaryRow, 7).Value = rows.Sum(r => r.TareWeight ?? 0);
        ws.Cell(summaryRow, 8).Value = rows.Sum(r => r.NetWeight ?? 0);

        workbook.SaveAs(outputPath);
    }
}
