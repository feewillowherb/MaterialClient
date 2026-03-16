using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     固废导出与数据源相关服务的集成注册扩展。
/// </summary>
public static class SolidWasteExportServiceCollectionExtensions
{
    /// <summary>
    ///     注册 SolidWasteService（唯一数据源）、通用 Excel 导出、固废 Excel 导出门面。
    /// </summary>
    public static IServiceCollection AddSolidWasteExportServices(this IServiceCollection services)
    {
        services.AddTransient<ISolidWasteService, SolidWasteService>();
        services.AddTransient<IExcelExportService, ExcelExportService>();
        services.AddTransient<ISolidWasteExcelExportService, SolidWasteExcelExportService>();
        return services;
    }
}
