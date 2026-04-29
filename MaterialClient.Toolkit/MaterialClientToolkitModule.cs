using MaterialClient.Common;
using MaterialClient.Common.Api;
using MaterialClient.Toolkit.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;
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
        var configuration = context.Services.GetConfiguration();

        var materialPlatformUrl = configuration["MaterialPlatform:BaseUrl"]
                                  ?? "http://mt.publicapi.findong.com";

        services.AddTransient<ToolkitBearerTokenHandler>();

        services.AddRefitClient<IMaterialPlatformApi>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(materialPlatformUrl);
                c.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<ToolkitBearerTokenHandler>();
    }
}

