using MaterialClient.Common;
using MaterialClient.Toolkit.Services;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;

namespace MaterialClient.Toolkit;

/// <summary>
/// MaterialClient.Toolkit ABP模块
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

