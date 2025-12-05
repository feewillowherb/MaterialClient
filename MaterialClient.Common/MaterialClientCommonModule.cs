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
            options.Configure(context =>
            {
                context.DbContextOptions.UseSqlite(connectionString)
                    .EnableDetailedErrors() // 启用详细的错误信息
                    .EnableSensitiveDataLogging(); // 启用敏感数据日志记录（包含参数值）
            });
        });

        // Register Refit API Client with retry policy and timeout
        var basePlatformUrl = configuration["BasePlatform:BaseUrl"]
                              ?? "http://base.publicapi.findong.com";

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

        // Register Authentication Services
        services.AddSingleton<IMachineCodeService, MachineCodeService>();
        services.AddSingleton<IPasswordEncryptionService, PasswordEncryptionService>();
        // ILicenseService and IAuthenticationService are auto-registered by ABP (ITransientDependency)

        // Register Services
        services.AddSingleton<HikvisionService>();

        // Register Hardware Services (singleton for test value persistence)
        services.AddSingleton<ITruckScaleWeightService, TruckScaleWeightService>();
        services.AddSingleton<IPlateNumberCaptureService, PlateNumberCaptureService>();
        services.AddSingleton<IVehiclePhotoService, VehiclePhotoService>();
        services.AddSingleton<IBillPhotoService, BillPhotoService>();
        services.AddSingleton<IHikvisionService, HikvisionService>();

        // Register WeighingService (transient, registered via ITransientDependency)
        // No need to register explicitly, ABP will auto-register it

        // Register SettingsService (transient, registered via ITransientDependency)
        // No need to register explicitly, ABP will auto-register it

        // Register AttendedWeighingService as singleton (needs to maintain state and listen continuously)
        services.AddSingleton<AttendedWeighingService>();

        // Repositories are automatically registered by ABP framework
        // when using IRepository<TEntity, TKey> interface
        // No manual registration needed for repositories
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

        // 配置 Serilog
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
            .MinimumLevel.Override("Volo.Abp", LogEventLevel.Warning)
            .WriteTo.File(
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                encoding: System.Text.Encoding.UTF8)
            .CreateLogger();

        // 将 Serilog 添加到日志提供程序
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSerilog(Log.Logger);
        });
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
            using var dbContext = await dbContextProvider.GetDbContextAsync();
            await dbContext.Database.MigrateAsync();
            await uow.CompleteAsync();
        }
        catch (Exception ex)
        {
            // 记录错误但不阻止应用启动
            var logger = context.ServiceProvider.GetService<ILogger<MaterialClientCommonModule>>();
            logger?.LogError(ex, "数据库迁移失败");
        }

        // 启动 AttendedWeighingService
        try
        {
            var attendedWeighingService = context.ServiceProvider.GetService<AttendedWeighingService>();
            if (attendedWeighingService != null)
            {
                await attendedWeighingService.StartAsync();
                var logger = context.ServiceProvider.GetService<ILogger<MaterialClientCommonModule>>();
                logger?.LogInformation("AttendedWeighingService 已自动启动");
            }
        }
        catch (Exception ex)
        {
            // 记录错误但不阻止应用启动
            var logger = context.ServiceProvider.GetService<ILogger<MaterialClientCommonModule>>();
            logger?.LogError(ex, "启动 AttendedWeighingService 失败");
        }
    }


    public override async Task OnApplicationShutdownAsync(ApplicationShutdownContext context)
    {
        // 确保 Serilog 正确关闭并刷新所有日志
        await Log.CloseAndFlushAsync();
        await base.OnApplicationShutdownAsync(context);
    }
}