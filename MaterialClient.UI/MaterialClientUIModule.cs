using System;
using MaterialClient.Common;
using MaterialClient.Common.Configuration;
using MaterialClient.Services;
using MaterialClient.UI.Views;
using MaterialClient.UI.Views.AttendedWeighing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace MaterialClient.UI;

[DependsOn(
    typeof(MaterialClientCommonModule),
    typeof(AbpAutofacModule)
)]
public class MaterialClientUIModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = context.Services.GetConfiguration();

        // Register Windows - MainWindow is singleton as it's main application window
        services.AddSingleton<MainWindow>();

        // Windows used by StartupService (created at startup, then shown/hidden)
        services.AddTransient<AuthCodeWindow>();
        services.AddTransient<LoginWindow>();
        services.AddTransient<AttendedWeighingWindow>();

        // Register startup service
        services.AddTransient<StartupService>();

        // Register Web Host service
        services.AddSingleton<MinimalWebHostService>();

        // Configure Streets
        services.Configure<StreetsConfig>(options =>
        {
            var streets = configuration.GetSection("Streets").Get<string[]>();
            options.Streets = streets ?? Array.Empty<string>();
        });

        // Configure SolidWasteTypes
        services.Configure<SolidWasteTypeConfig>(options =>
        {
            var solidWasteTypes = configuration.GetSection("SolidWasteTypes").Get<string[]>();
            options.SolidWasteTypes = solidWasteTypes ?? Array.Empty<string>();
        });
    }
}
