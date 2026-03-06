using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MaterialClient.Common;
using MaterialClient.Common.Api;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Providers;
using MaterialClient.Common.Services;
using MaterialClient.EFCore;
using MaterialClient.Services;
using MaterialClient.UI.ViewModels;
using MaterialClient.UI.Views;
using MaterialClient.UI.Views.AttendedWeighing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Refit;
using Serilog;
using Serilog.Events;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace MaterialClient;

[DependsOn(
    typeof(MaterialClientCommonModule),
    typeof(AbpAutofacModule),
    typeof(AbpBackgroundWorkersModule)
)]
public class MaterialClientModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // Load additional configuration files
        var existingConfig = context.Services.GetConfiguration();
        if (existingConfig != null)
        {
            var appDirectory = AppContext.BaseDirectory;
            var configBuilder = new ConfigurationBuilder();

            // Add existing configuration (includes appsettings.json loaded by ABP)
            configBuilder.AddConfiguration(existingConfig);

            // Add appsettings.product.json if it exists (optional, will override appsettings.json values)
            var productConfigPath = Path.Combine(appDirectory, "appsettings.secret.json");
            if (File.Exists(productConfigPath))
            {
                configBuilder.AddJsonFile(productConfigPath, optional: true, reloadOnChange: true);
            }

            #if DEBUG
            // Add User Secrets as last source (highest priority, overrides all config files)
            configBuilder.AddUserSecrets<MaterialClientModule>();
            #endif

            var enhancedConfig = configBuilder.Build();
            context.Services.ReplaceConfiguration(enhancedConfig);
        }
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = context.Services.GetConfiguration();

        // 配置 Serilog 日志
        ConfigureSerilog(services, configuration);

        // Register Refit API Client with retry policy and timeout
        var basePlatformUrl = configuration["BasePlatform:BaseUrl"]
                              ?? "http://localhost:5000";

        services.AddRefitClient<IBasePlatformApi>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(basePlatformUrl);
                c.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddTransientHttpErrorPolicy(policy =>
                policy.WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                ));

        // Register Material Platform Refit API Client with bearer token handler
        var materialPlatformUrl = configuration["MaterialPlatform:BaseUrl"]
                                  ?? basePlatformUrl;

        services.AddTransient<MaterialPlatformBearerTokenHandler>();

        services.AddRefitClient<IMaterialPlatformApi>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(materialPlatformUrl);
                c.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<MaterialPlatformBearerTokenHandler>()
            .AddTransientHttpErrorPolicy(policy =>
                policy.WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                ));

        // Register Windows
        // MainWindow is singleton as it's main application window
        services.AddSingleton<MaterialClient.UI.Views.MainWindow>();

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

    private void ConfigureSerilog(IServiceCollection services, IConfiguration configuration)
    {
        // 获取应用程序 exe 目录
        var appDirectory = AppContext.BaseDirectory;
        var logsDirectory = Path.Combine(appDirectory, "Logs");

        // 确保 Logs 目录存在
        if (!Directory.Exists(logsDirectory)) Directory.CreateDirectory(logsDirectory);

        // 配置日志文件路径，按日期滚动
        var logFilePath = Path.Combine(logsDirectory, "MaterialClient-.log");

        // 从配置文件读取日志级别
        var defaultLevel = GetLogLevel(configuration, "Logging:LogLevel:Default", "Information");
        var microsoftLevel = GetLogLevel(configuration, "Logging:LogLevel:Microsoft", "Warning");
        var efCoreLevel = GetLogLevel(configuration, "Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
        var abpLevel = GetLogLevel(configuration, "Logging:LogLevel:Volo.Abp", "Warning");

        // 配置 Serilog
        var loggerConfig = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Is(ParseLogEventLevel(defaultLevel))
            .MinimumLevel.Override("Microsoft", ParseLogEventLevel(microsoftLevel))
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", ParseLogEventLevel(efCoreLevel))
            .MinimumLevel.Override("Volo.Abp", ParseLogEventLevel(abpLevel))
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                encoding: Encoding.UTF8);

        Log.Logger = loggerConfig.CreateLogger();

        // 将 Serilog 添加到日志提供程序
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSerilog(Log.Logger);
        });
    }

    private string GetLogLevel(IConfiguration configuration, string key, string defaultValue)
    {
        return configuration[key] ?? defaultValue;
    }

    private LogEventLevel ParseLogEventLevel(string level)
    {
        return Enum.TryParse<LogEventLevel>(level, true, out var result)
            ? result
            : LogEventLevel.Information;
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        // 尝试自动更新数据库迁移
        try
        {
            var unitOfWorkManager = context.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
            var dbContextProvider =
                context.ServiceProvider.GetRequiredService<IDbContextProvider<MaterialClientDbContext>>();
            using var uow = unitOfWorkManager.Begin(true, false);
            await using var dbContext = await dbContextProvider.GetDbContextAsync();
            await dbContext.Database.MigrateAsync();
            await uow.CompleteAsync();
        }
        catch (Exception ex)
        {
            // 记录错误但不阻止应用启动
            var logger = context.ServiceProvider.GetService<ILogger<MaterialClientModule>>();
            logger?.LogError(ex, "数据库迁移失败");
        }

        // 注册并启动后台工作器（可通过配置禁用）
        // Note: PollingBackgroundService is temporarily disabled as it needs proper placement
        var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
        var pollingEnabled = configuration.GetValue("BackgroundServices:Polling", true);
        if (pollingEnabled)
        {
            // await context.AddBackgroundWorkerAsync<PollingBackgroundService>();
            var logger = context.ServiceProvider.GetService<ILogger<MaterialClientModule>>();
            logger?.LogWarning("PollingBackgroundService registration is disabled pending proper implementation");
        }
        else
        {
            var logger = context.ServiceProvider.GetService<ILogger<MaterialClientModule>>();
            logger?.LogInformation("PollingBackgroundService is disabled by configuration (BackgroundServices:Polling=false).");
        }

        // 初始化车牌号推荐服务缓存
        try
        {
            var recommendPlateNumberService =
                context.ServiceProvider.GetRequiredService<RecommendPlateNumberService>();
            await recommendPlateNumberService.InitializeCacheAsync();
        }
        catch (Exception ex)
        {
            // 记录错误但不阻止应用启动
            var logger = context.ServiceProvider.GetService<ILogger<MaterialClientModule>>();
            logger?.LogError(ex, "初始化车牌号推荐服务缓存失败");
        }
    }

    public override async Task OnApplicationShutdownAsync(ApplicationShutdownContext context)
    {
        // 确保 Serilog 正确关闭并刷新所有日志
        await Log.CloseAndFlushAsync();
        await base.OnApplicationShutdownAsync(context);
    }
}
