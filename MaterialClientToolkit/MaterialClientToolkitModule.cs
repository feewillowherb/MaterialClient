using MaterialClient.Common;
using MaterialClientToolkit.Services;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;

namespace MaterialClientToolkit;

/// <summary>
/// MaterialClientToolkit ABP模块
/// </summary>
[DependsOn(
    typeof(MaterialClientCommonModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
)]
public class MaterialClientToolkitModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 注册服务
        // DbContext配置已在MaterialClientCommonModule中完成
        services.AddTransient<CsvReaderService>();
        services.AddTransient<CsvMapperService>();
        services.AddTransient<DatabaseMigrationService>();
    }
}

