using System.Text;
using MaterialClient.Common;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.AttendedWeighing;
using MaterialClient.EFCore;
using MaterialClient.Urban.Services;
using MaterialClient.Urban.ViewModels;
using MaterialClient.Urban.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace MaterialClient.Urban;

[DependsOn(
    typeof(MaterialClientCommonModule),
    typeof(AbpAutofacModule)
)]
public class MaterialClientUrbanModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = context.Services.GetConfiguration();

        // Configure Serilog logging with daily rotation (matching MaterialClient pattern)
        ConfigureSerilog(services, configuration);

        // Register IWeighingPipelineStrategy -> UrbanWeighingPipelineStrategy
        services.AddSingleton<IWeighingPipelineStrategy, UrbanWeighingPipelineStrategy>();

        // Register Urban window (singleton) and ViewModel (transient - resolved per window)
        services.AddSingleton<UrbanAttendedWeighingWindow>();
        services.AddTransient<UrbanAttendedWeighingViewModel>();
    }

    private void ConfigureSerilog(IServiceCollection services, IConfiguration configuration)
    {
        var appDirectory = AppContext.BaseDirectory;
        var logsDirectory = Path.Combine(appDirectory, "Logs");

        if (!Directory.Exists(logsDirectory))
            Directory.CreateDirectory(logsDirectory);

        var logFilePath = Path.Combine(logsDirectory, "MaterialClient.Urban-.log");

        var defaultLevel = GetLogLevel(configuration, "Logging:LogLevel:Default", "Information");
        var microsoftLevel = GetLogLevel(configuration, "Logging:LogLevel:Microsoft", "Warning");
        var efCoreLevel = GetLogLevel(configuration, "Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
        var abpLevel = GetLogLevel(configuration, "Logging:LogLevel:Volo.Abp", "Warning");

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

        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSerilog(Log.Logger);
        });
    }

    private string GetLogLevel(IConfiguration configuration, string key, string defaultValue)
        => configuration[key] ?? defaultValue;

    private LogEventLevel ParseLogEventLevel(string level)
        => Enum.TryParse<LogEventLevel>(level, true, out var result)
            ? result
            : LogEventLevel.Information;

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var logger = context.ServiceProvider.GetService<ILogger<MaterialClientUrbanModule>>();

        // Execute database migration
        try
        {
            var unitOfWorkManager = context.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
            var dbContextProvider =
                context.ServiceProvider.GetRequiredService<IDbContextProvider<MaterialClientDbContext>>();

            using var uow = unitOfWorkManager.Begin(true, false);
            await using var dbContext = await dbContextProvider.GetDbContextAsync();
            await dbContext.Database.MigrateAsync();
            await uow.CompleteAsync();

            logger?.LogInformation("Database migration completed successfully");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Database migration failed");
        }

        // Execute static license check (non-blocking on failure)
        try
        {
            var licenseChecker = context.ServiceProvider.GetRequiredService<IStaticLicenseChecker>();
            var settings = new SystemSettings();
            var result = await licenseChecker.CheckLicenseAsync(settings.LicenseFilePath);
            logger?.LogInformation("Static license check: {Status} - {Message}",
                result.IsSuccess ? "Passed" : "Failed", result.Message);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Static license check failed (non-blocking)");
        }
    }

    public override async Task OnApplicationShutdownAsync(ApplicationShutdownContext context)
    {
        // Flush and close Serilog
        await Log.CloseAndFlushAsync();
        await base.OnApplicationShutdownAsync(context);
    }
}
