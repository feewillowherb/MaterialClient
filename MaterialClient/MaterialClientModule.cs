using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using MaterialClient.Common;
using MaterialClient.Services;
using MaterialClient.ViewModels;
using MaterialClient.Views;

namespace MaterialClient;

[DependsOn(
    typeof(MaterialClientCommonModule),
    typeof(AbpAutofacModule)
)]
public class MaterialClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = context.Services.GetConfiguration();

        // Register Windows
        // MainWindow is singleton as it's the main application window
        services.AddSingleton<MainWindow>();
        // Login and Auth windows are transient as they can be shown multiple times
        services.AddTransient<LoginWindow>();
        services.AddTransient<AuthCodeWindow>();
        services.AddTransient<AttendedWeighingWindow>();
        services.AddTransient<SettingsWindow>();

        // Register ViewModels (transient as they are bound to specific UI instances)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<LoginWindowViewModel>();
        services.AddTransient<AuthCodeWindowViewModel>();
        services.AddTransient<AttendedWeighingViewModel>();
        services.AddTransient<SettingsWindowViewModel>();

        // Register startup service
        services.AddTransient<StartupService>();
    }
}

