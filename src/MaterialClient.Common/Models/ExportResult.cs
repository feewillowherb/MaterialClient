namespace MaterialClient.Common.Models;

public class ExportResult
{
    public int RowCount { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public bool Success { get; set; }
}
