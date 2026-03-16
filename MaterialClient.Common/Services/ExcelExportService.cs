using ClosedXML.Excel;

namespace MaterialClient.Common.Services;

/// <summary>
///     通用 Excel 导出：按表头与行数据写入 .xlsx，与业务无关。
/// </summary>
public interface IExcelExportService
{
    /// <summary>
    ///     将表头与数据行写入指定路径的 .xlsx 文件。
    /// </summary>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="headers">表头文本数组</param>
    /// <param name="rows">数据行集合</param>
    /// <param name="rowToValues">将每行转换为列值数组（长度应与 headers 一致）</param>
    /// <param name="getSummaryRow">可选：根据已写入的行集合返回汇总行列值，若返回 null 则不追加汇总行</param>
    Task WriteAsync<T>(
        string outputPath,
        string[] headers,
        IEnumerable<T> rows,
        Func<T, object?[]?> rowToValues,
        Func<IReadOnlyList<T>, object?[]?>? getSummaryRow = null);
}

public class ExcelExportService : IExcelExportService
{
    public Task WriteAsync<T>(
        string outputPath,
        string[] headers,
        IEnumerable<T> rows,
        Func<T, object?[]?> rowToValues,
        Func<IReadOnlyList<T>, object?[]?>? getSummaryRow = null)
    {
        var rowList = rows.ToList();
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sheet1");

        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var colCount = headers.Length;
        var r = 0;
        foreach (var row in rowList)
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
            var summaryValues = getSummaryRow(rowList);
            if (summaryValues != null && summaryValues.Length > 0)
            {
                var summaryRowIndex = rowList.Count + 2;
                for (var c = 0; c < summaryValues.Length && c < colCount; c++)
                {
                    SetCellValue(ws.Cell(summaryRowIndex, c + 1), summaryValues[c]);
                }
            }
        }

        workbook.SaveAs(outputPath);
        return Task.CompletedTask;
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
