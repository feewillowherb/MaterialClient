using System.Threading.Tasks;
using MaterialClient.Backgrounds;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using MaterialClient.Common;
using MaterialClient.Services;
using MaterialClient.ViewModels;
using MaterialClient.Views;
using MaterialClient.Views.AttendedWeighing;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;

namespace MaterialClient;

[DependsOn(
    typeof(MaterialClientCommonModule),
    typeof(AbpAutofacModule),
    typeof(AbpBackgroundWorkersModule)
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
        // AttendedWeighingDetailView 需要 WeighingRecord 参数，在使用时通过 IServiceProvider 创建

        // Register ViewModels (transient as they are bound to specific UI instances)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<LoginWindowViewModel>();
        services.AddTransient<AuthCodeWindowViewModel>();
        services.AddTransient<AttendedWeighingViewModel>();
        services.AddTransient<SettingsWindowViewModel>();
        // AttendedWeighingDetailViewModel 需要 WeighingRecord 参数，在使用时创建

        // Register startup service
        services.AddTransient<StartupService>();

        // Register Web Host service
        services.AddSingleton<MinimalWebHostService>();
        
    }
    
    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        // 注册并启动后台工作器
        await context.AddBackgroundWorkerAsync<PollingBackgroundService>();
    }
}