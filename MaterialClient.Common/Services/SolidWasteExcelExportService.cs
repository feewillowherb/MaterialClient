using MaterialClient.Common.Models;
using Microsoft.Extensions.Logging;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

public interface ISolidWasteExcelExportService
{
    Task<ExportResult> ExportAsync(SolidWasteExportFilter filter, string outputPath);
}

[AutoConstructor]
public partial class SolidWasteExcelExportService : ISolidWasteExcelExportService
{
    private readonly ISolidWasteService _solidWasteService;
    private readonly IExcelExportService _excelExportService;
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
            var rows = await _solidWasteService.GetExportRowsAsync(filter);
            var rowList = rows.ToList();

            await _excelExportService.WriteAsync(
                outputPath,
                Headers,
                rowList,
                RowToValues,
                GetSummaryRow);

            return new ExportResult
            {
                RowCount = rowList.Count,
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

    private static object?[]? RowToValues(SolidWasteExportRow row)
    {
        return
        [
            row.SerialNumber,
            row.VehicleNumber,
            row.ShippingUnit,
            row.ReceivingUnit,
            row.GoodsName,
            row.GrossWeight ?? 0,
            row.TareWeight ?? 0,
            row.NetWeight ?? 0,
            row.Remark,
            row.GrossWeightTime,
            row.TareWeightTime,
            row.Street,
            row.SolidWasteType,
            row.ManifestNumber,
            row.UploadResult,
            row.UploadStatus,
            row.UploadTime
        ];
    }

    private static object?[]? GetSummaryRow(IReadOnlyList<SolidWasteExportRow> rows)
    {
        // 汇总行：第 1 列为总数，第 6/7/8 列为毛重/皮重/净重之和，其余列为空（共 17 列）
        var arr = new object?[17];
        arr[0] = rows.Count;
        arr[5] = rows.Sum(r => r.GrossWeight ?? 0);
        arr[6] = rows.Sum(r => r.TareWeight ?? 0);
        arr[7] = rows.Sum(r => r.NetWeight ?? 0);
        return arr;
    }
}
