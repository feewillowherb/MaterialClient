namespace MaterialClient.Common.Models;

/// <summary>
///     固废运单分页查询结果。
/// </summary>
public record PagedSolidWasteResult(IReadOnlyList<SolidWasteExportRow> Items, int TotalCount);
