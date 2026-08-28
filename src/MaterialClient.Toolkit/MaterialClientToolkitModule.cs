using MaterialClient.Common;
using MaterialClient.Common.EntityFrameworkCore;
using MaterialClient.Toolkit.Services;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;

namespace MaterialClient.Toolkit;

[DependsOn(
    typeof(MaterialClientCommonModule),
    typeof(MaterialClientEntityFrameworkCoreModule)
)]
public class MaterialClientToolkitModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        
    }
}

