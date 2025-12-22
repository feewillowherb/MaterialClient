using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.Modularity;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Uow;
using MaterialClient.EFCore;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Common.Services;
using MaterialClient.Common.Api;
using Refit;
using Polly;
using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp;
using Serilog;
using Serilog.Events;
using Yitter.IdGenerator;

namespace MaterialClient.Common;

[DependsOn(
    typeof(AbpEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
)]
public class MaterialClientCommonModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = context.Services.GetConfiguration();

        // 配置 Serilog 日志
        ConfigureSerilog(services, configuration);

        // Register DbContext with default repositories
        services.AddAbpDbContext<MaterialClientDbContext>(options =>
        {
            // Enable default repositories for all entities
            options.AddDefaultRepositories(includeAllEntities: true);
        });

        // Configure SQLite connection from configuration
        var connectionString = configuration.GetConnectionString("Default")
                               ?? "Data Source=MaterialClient.db";

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(c =>
            {
                c.DbContextOptions.UseSqlite(connectionString)
                    .EnableDetailedErrors() // 启用详细的错误信息
                    .EnableSensitiveDataLogging(); // 启用敏感数据日志记录（包含参数值）
            });
        });

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
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
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
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                ));

        var options = new IdGeneratorOptions(1);
        // 2. 保存配置并初始化
        YitIdHelper.SetIdGenerator(options);
    }

    private void ConfigureSerilog(IServiceCollection services, IConfiguration configuration)
    {
        // 获取应用程序 exe 目录
        var appDirectory = AppContext.BaseDirectory;
        var logsDirectory = Path.Combine(appDirectory, "Logs");

        // 确保 Logs 目录存在
        if (!Directory.Exists(logsDirectory))
        {
            Directory.CreateDirectory(logsDirectory);
        }

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
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                encoding: System.Text.Encoding.UTF8);

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
        return Enum.TryParse<LogEventLevel>(level, ignoreCase: true, out var result)
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

            using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
            await using var dbContext = await dbContextProvider.GetDbContextAsync();
            await dbContext.Database.MigrateAsync();
            await uow.CompleteAsync();
        }
        catch (Exception ex)
        {
            // 记录错误但不阻止应用启动
            var logger = context.ServiceProvider.GetService<ILogger<MaterialClientCommonModule>>();
            logger?.LogError(ex, "数据库迁移失败");
        }
    }


    public override async Task OnApplicationShutdownAsync(ApplicationShutdownContext context)
    {
        // 确保 Serilog 正确关闭并刷新所有日志
        await Log.CloseAndFlushAsync();
        await base.OnApplicationShutdownAsync(context);
    }
}