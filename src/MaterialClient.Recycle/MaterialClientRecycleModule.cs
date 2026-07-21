using System;
using System.IO;
using MaterialClient.AttendedWeighing;
using MaterialClient.Common;
using MaterialClient.Common.Api;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Logging;
using MaterialClient.Common.Services;
using MaterialClient.EFCore;
using MaterialClient.Recycle.Api;
using MaterialClient.Recycle.Backgrounds;
using MaterialClient.Recycle.Models;
using MaterialClient.Recycle.Services;
using MaterialClient.Recycle.ViewModels;
using MaterialClient.UI.ViewModels;
using MaterialClient.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;
using Serilog;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace MaterialClient.Recycle;

[DependsOn(
    typeof(MaterialClientCommonModule),
    typeof(MaterialClientUiModule),
    typeof(MaterialClientAttendedWeighingModule),
    typeof(AbpAutofacModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(AbpCachingModule)
)]
public class MaterialClientRecycleModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // 加载额外配置文件（appsettings.secret.json、User Secrets），与 MaterialClient/Urban 模式一致。
        var existingConfig = context.Services.GetConfiguration();
        if (existingConfig != null)
        {
            var appDirectory = AppContext.BaseDirectory;
            var configBuilder = new ConfigurationBuilder();

            configBuilder.AddConfiguration(existingConfig);

            var secretConfigPath = Path.Combine(appDirectory, "appsettings.secret.json");
            if (File.Exists(secretConfigPath))
            {
                configBuilder.AddJsonFile(secretConfigPath, optional: true, reloadOnChange: true);
            }

#if DEBUG
            configBuilder.AddUserSecrets<MaterialClientRecycleModule>();
#endif

            var enhancedConfig = configBuilder.Build();
            context.Services.ReplaceConfiguration(enhancedConfig);
        }
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = context.Services.GetConfiguration();

        // Serilog 日志（按日轮转）
        SerilogFileLogConfigurator.Configure(services, configuration, "MaterialClient.Recycle");

        // Recycle 授权窗口固定 ProductCode 5020（覆盖默认 AuthCodeWindowViewModel）
        services.AddTransient<AuthCodeWindowViewModel, RecycleAuthCodeWindowViewModel>();

        services.AddHttpClient();

        // 共享层 Refit 客户端（IBasePlatformApi、IMaterialPlatformApi，供 LicenseService 等 Common 服务使用）
        services.AddMaterialClientRefitClients(configuration);

        // RecycleSync 配置段绑定
        services.Configure<RecycleSyncOptions>(configuration.GetSection("RecycleSync"));

        // RecycleHmacDelegatingHandler 需注册为瞬时，供 AddHttpMessageHandler 解析。
        services.AddTransient<RecycleHmacDelegatingHandler>();

        // 注册 IRecycleDataApi Refit 客户端：BaseAddress 绑定 RecycleSync:ApiUrl，
        // Timeout 绑定 RecycleSync:TimeoutSeconds，并附加 HMAC 签名 Handler。
        var recycleSync = configuration.GetSection("RecycleSync");
        var apiUrl = recycleSync["ApiUrl"] ?? "http://localhost";
        var timeoutSeconds = recycleSync.GetValue("TimeoutSeconds", 30);

        services.AddRefitClient<IRecycleDataApi>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(apiUrl);
                c.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            })
            .AddHttpMessageHandler<RecycleHmacDelegatingHandler>();

        // 诊断 Web Host：通过 MinimalWebHost 配置节统一控制（Urls / EnableOnStartup）
        services.AddSingleton<MinimalWebHostService>();
        services.AddSingleton<IMinimalWebHostService>(sp => sp.GetRequiredService<MinimalWebHostService>());
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var logger = context.ServiceProvider.GetService<ILogger<MaterialClientRecycleModule>>();

        await RunDatabaseMigrationAsync(context.ServiceProvider, logger);

        await EnsureRecycleDefaultWeighingModeAsync(context.ServiceProvider, logger);

        var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
        var recycleEnabled = configuration.GetValue("RecycleSync:Enabled", true);
        if (recycleEnabled)
        {
            await context.AddBackgroundWorkerAsync<RecyclePollingBackgroundService>();
            logger?.LogInformation("Recycle PollingBackgroundService 已注册（RecycleSync:Enabled=true）。");
        }
        else
        {
            logger?.LogInformation("Recycle PollingBackgroundService 未启用（RecycleSync:Enabled=false）。");
        }
    }

    public override async Task OnApplicationShutdownAsync(ApplicationShutdownContext context)
    {
        // 停止后台 Worker 由 ABP 框架在 Shutdown 时统一处理。

        // Flush 并关闭 Serilog
        await Log.CloseAndFlushAsync();
        await base.OnApplicationShutdownAsync(context);
    }

    private static async Task RunDatabaseMigrationAsync(
        IServiceProvider serviceProvider,
        ILogger<MaterialClientRecycleModule>? logger)
    {
        try
        {
            var unitOfWorkManager = serviceProvider.GetRequiredService<IUnitOfWorkManager>();
            var dbContextProvider =
                serviceProvider.GetRequiredService<IDbContextProvider<MaterialClientDbContext>>();

            using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
            var dbContext = await dbContextProvider.GetDbContextAsync();
            await dbContext.Database.MigrateAsync();
            await uow.CompleteAsync();

            logger?.LogInformation("Recycle 数据库迁移完成。");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Recycle 数据库迁移失败。");
        }
    }

    private static async Task EnsureRecycleDefaultWeighingModeAsync(
        IServiceProvider serviceProvider,
        ILogger<MaterialClientRecycleModule>? logger)
    {
        try
        {
            var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
            var settings = await settingsService.GetSettingsAsync();
            var systemSettings = settings.SystemSettings;

            if (systemSettings.DefaultWeighingMode == WeighingMode.Recycle)
            {
                logger?.LogInformation("Recycle 默认称重模式已配置。");
                return;
            }

            systemSettings.DefaultWeighingMode = WeighingMode.Recycle;
            settings.SystemSettings = systemSettings;
            await settingsService.SaveSettingsAsync(settings);

            logger?.LogInformation("Recycle 默认称重模式已设置为 {Mode}。", systemSettings.DefaultWeighingMode);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "设置 Recycle 默认称重模式失败，启动继续。");
        }
    }
}
