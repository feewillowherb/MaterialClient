using MaterialClient.Common.Models;

namespace MaterialClient.Common.Services;

public interface ISolidWasteExcelExportService
{
    Task<ExportResult> ExportAsync(SolidWasteExportFilter filter, string outputPath);
}
