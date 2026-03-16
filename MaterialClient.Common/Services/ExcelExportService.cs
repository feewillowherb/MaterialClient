using ClosedXML.Excel;
using MaterialClient.Common.Models;
using Microsoft.Extensions.Logging;
using Volo.Abp.Uow;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

public interface c
{
    Task<ExportResult> ExportAsync(SolidWasteExportFilter filter, string outputPath);
}

public interface IExcelExportService
{
    Task<ExportResult> ExportSolidWasteAsync(SolidWasteExportFilter filter, string outputPath);
}

[AutoConstructor]
public partial class ExcelExportService : IExcelExportService, ISolidWasteExcelExportService, ITransientDependency
{
    private readonly ISolidWasteService _solidWasteService;
    private readonly ILogger<ExcelExportService> _logger;

    private static readonly string[] SolidWasteHeaders =
    [
        "流水号", "车  号", "发货单位", "收货单位", "货  名",
        "毛  重", "皮  重", "净  重", "备 注", "毛重时间",
        "皮重时间", "所属街道", "类型", "联单编号",
        "上传结果", "上传状态", "上传时间"
    ];

    [UnitOfWork]
    public virtual Task<ExportResult> ExportSolidWasteAsync(SolidWasteExportFilter filter, string outputPath)
        => ExportAsync(filter, outputPath);

    [UnitOfWork]
    public virtual async Task<ExportResult> ExportAsync(SolidWasteExportFilter filter, string outputPath)
    {
        try
        {
            var rows = await _solidWasteService.GetExportRowsAsync(filter);
            var rowList = rows.ToList();

            WriteWorksheet(
                outputPath,
                SolidWasteHeaders,
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

    private static void WriteWorksheet<T>(
        string outputPath,
        string[] headers,
        IReadOnlyList<T> rows,
        Func<T, object?[]?> rowToValues,
        Func<IReadOnlyList<T>, object?[]?>? getSummaryRow = null)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sheet1");

        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var colCount = headers.Length;
        var r = 0;
        foreach (var row in rows)
        {
            var values = rowToValues(row);
            if (values == null) continue;
            var excelRow = r + 2;
            for (var c = 0; c < colCount && c < values.Length; c++)
            {
                SetCellValue(ws.Cell(excelRow, c + 1), values[c]);
            }
            r++;
        }

        if (getSummaryRow != null)
        {
            var summaryValues = getSummaryRow(rows);
            if (summaryValues != null && summaryValues.Length > 0)
            {
                var summaryRowIndex = rows.Count + 2;
                for (var c = 0; c < summaryValues.Length && c < colCount; c++)
                {
                    SetCellValue(ws.Cell(summaryRowIndex, c + 1), summaryValues[c]);
                }
            }
        }

        workbook.SaveAs(outputPath);
    }

    private static void SetCellValue(IXLCell cell, object? v)
    {
        if (v == null)
        {
            cell.Value = string.Empty;
            return;
        }
        switch (v)
        {
            case string s:
                cell.Value = s;
                break;
            case double d:
                cell.Value = d;
                break;
            case decimal dec:
                cell.Value = (double)dec;
                break;
            case int i:
                cell.Value = i;
                break;
            case long l:
                cell.Value = l;
                break;
            case DateTime dt:
                cell.Value = dt;
                break;
            case bool b:
                cell.Value = b;
                break;
            default:
                cell.Value = v.ToString() ?? string.Empty;
                break;
        }
    }
}
