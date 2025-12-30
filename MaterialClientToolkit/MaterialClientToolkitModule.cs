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
    typeof(MaterialClientCommonModule)
)]
public class MaterialClientToolkitModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        
    }
}

