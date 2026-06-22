using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MaterialClient.Backgrounds;
using MaterialClient.Common;
using MaterialClient.UI;
using MaterialClient.Common.Api;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Providers;
using MaterialClient.Common.Services;
using MaterialClient.EFCore;
using MaterialClient.Services;
using MaterialClient.ViewModels;
using MaterialClient.Views;
using MaterialClient.Views.AttendedWeighing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace MaterialClient
{
[DependsOn(
    typeof(MaterialClientCommonModule),
    typeof(MaterialClientUiModule),
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
            // Add User Secrets as the last source (highest priority, overrides all config files)
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

        services.AddMaterialClientRefitClients(configuration);

        // Register Windows
        // MainWindow is singleton as it's the main application window
        services.AddSingleton<MainWindow>();
        

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
        // Check if logging is enabled (default: true)
        var logEnabled = configuration.GetValue<bool>("Log:Enabled", true);
        if (!logEnabled)
        {
            // Skip logging configuration if disabled
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug(); // Fallback to debug output
            });
            return;
        }

        // 获取应用程序 exe 目录
        var appDirectory = AppContext.BaseDirectory;
        var logDirectory = configuration.GetValue<string>("Log:Directory", "Logs");
        var logsDirectory = Path.IsPathRooted(logDirectory)
            ? logDirectory
            : Path.Combine(appDirectory, logDirectory);

        // 确保 Logs 目录存在
        if (!Directory.Exists(logsDirectory)) Directory.CreateDirectory(logsDirectory);

        // 配置日志文件路径，按日期滚动到日期目录结构
        var useDateFolders = configuration.GetValue<bool>("Log:UseDateFolders", true);
        var logFilePath = useDateFolders
            ? Path.Combine(logsDirectory, "{YYYY}/{MM}/{DD}/MaterialClient-.log")
            : Path.Combine(logsDirectory, "MaterialClient-.log");

        // 从配置文件读取日志级别
        var defaultLevel = GetLogLevel(configuration, "Logging:LogLevel:Default", "Information");
        var microsoftLevel = GetLogLevel(configuration, "Logging:LogLevel:Microsoft", "Warning");
        var efCoreLevel = GetLogLevel(configuration, "Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
        var abpLevel = GetLogLevel(configuration, "Logging:LogLevel:Volo.Abp", "Warning");

        // 读取文件大小限制和保留天数
        var fileSizeLimitMB = configuration.GetValue<int>("Log:FileSizeLimitMB", 50);
        var retentionDays = configuration.GetValue<int>("Log:RetentionDays", 30);

        // 验证文件大小限制范围（10-500MB）
        if (fileSizeLimitMB < 10 || fileSizeLimitMB > 500)
        {
            Log.Warning($"Invalid Log:FileSizeLimitMB value: {fileSizeLimitMB}. Using default 50MB.");
            fileSizeLimitMB = 50;
        }

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
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: fileSizeLimitMB * 1024 * 1024,  // Convert MB to bytes
                retainedFileCountLimit: retentionDays > 0 ? null : (int?)null,  // Serilog's day-based retention
                retainedFileTimeLimit: retentionDays > 0 ? (TimeSpan?)TimeSpan.FromDays(retentionDays) : null,
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
        var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();

        var pollingEnabled = configuration.GetValue("BackgroundServices:Polling", true);
        if (pollingEnabled)
        {
            await context.AddBackgroundWorkerAsync<PollingBackgroundService>();
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
}