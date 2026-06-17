using System.Text;
using MaterialClient.Common;
using MaterialClient.Common.Api;
using MaterialClient.UI;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.AttendedWeighing;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.EFCore;
using MaterialClient.Urban.Api;
using MaterialClient.Urban.Services;
using MaterialClient.Urban.ViewModels;
using MaterialClient.Urban.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Refit;
using Serilog;
using Serilog.Events;
using MaterialClient.Urban.Backgrounds;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EventBus;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace MaterialClient.Urban
{
[DependsOn(
    typeof(MaterialClientCommonModule),
    typeof(MaterialClientUiModule),
    typeof(AbpAutofacModule),
    typeof(AbpBackgroundWorkersModule)
)]
public class MaterialClientUrbanModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // Load additional configuration files (matching MaterialClient pattern)
        var existingConfig = context.Services.GetConfiguration();
        if (existingConfig != null)
        {
            var appDirectory = AppContext.BaseDirectory;
            var configBuilder = new ConfigurationBuilder();

            // Add existing configuration (includes appsettings.json loaded by ABP)
            configBuilder.AddConfiguration(existingConfig);

            // Add appsettings.secret.json if it exists (optional, will override appsettings.json values)
            var secretConfigPath = Path.Combine(appDirectory, "appsettings.secret.json");
            if (File.Exists(secretConfigPath))
            {
                configBuilder.AddJsonFile(secretConfigPath, optional: true, reloadOnChange: true);
            }

            #if DEBUG
            // Add User Secrets as the last source (highest priority, overrides all config files)
            configBuilder.AddUserSecrets<MaterialClientUrbanModule>();
            #endif

            var enhancedConfig = configBuilder.Build();
            context.Services.ReplaceConfiguration(enhancedConfig);
        }
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = context.Services.GetConfiguration();

        // Configure Serilog logging with daily rotation (matching MaterialClient pattern)
        ConfigureSerilog(services, configuration);

        // Register IHttpClientFactory for services that depend on it (e.g., SoundDeviceService)
        services.AddHttpClient();

        // Refit API clients (IBasePlatformApi, IMaterialPlatformApi — required by AttachmentService, etc.)
        services.AddMaterialClientRefitClients(configuration);

        // Register IUrbanManagementApi Refit client
        var urbanManagementUrl = configuration["UrbanManagement:BaseUrl"] ?? "http://localhost:5000";
        services.AddRefitClient<IUrbanManagementApi>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(urbanManagementUrl);
                c.Timeout = TimeSpan.FromSeconds(30);
            });

        // Configure SignalR client options
        services.Configure<SignalRClientOptions>(configuration.GetSection("SignalR"));

        // Confirm ILocalEventBus is available (ABP registers this automatically)
        // DeviceStatusEventHandler uses ITransientDependency for auto-registration
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

        await EnsureUrbanDefaultWeighingModeAsync(context.ServiceProvider, logger);

        var startupAuthService = context.ServiceProvider.GetRequiredService<IUrbanStartupAuthorizationService>();
        var isAuthorized = await TryExecuteStartupLicenseCheckAsync(context, logger);
        startupAuthService.SetResult(isAuthorized);

        if (!startupAuthService.IsAuthorized)
        {
            logger?.LogWarning(
                "Startup authorization failed: {Message}. Main window and background services will not start.",
                startupAuthService.Result.FailureMessage);
            return;
        }

        // Start SignalR client connection
        try
        {
            var signalRClient = context.ServiceProvider.GetService<IDeviceStatusSignalRClient>();
            if (signalRClient != null)
            {
                _ = signalRClient.StartAsync();
                logger?.LogInformation("SignalR client connection initiated");
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "SignalR client start failed");
        }

        var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
        var pollingEnabled = configuration.GetValue("BackgroundServices:Polling", true);
        if (pollingEnabled)
        {
            await context.AddBackgroundWorkerAsync<PollingBackgroundService>();
            logger?.LogInformation("Urban PollingBackgroundService registered (BackgroundServices:Polling=true).");
        }
        else
        {
            logger?.LogInformation(
                "Urban PollingBackgroundService is disabled by configuration (BackgroundServices:Polling=false).");
        }
    }

    private static async Task<UrbanStartupAuthorizationResult> TryExecuteStartupLicenseCheckAsync(
        ApplicationInitializationContext context,
        ILogger<MaterialClientUrbanModule>? logger)
    {
        // Priority: LicenseInfo.LatestJwtToken (server JWT) > license.urban file (bootstrap)
        try
        {
            var licenseChecker = context.ServiceProvider.GetRequiredService<IStaticLicenseChecker>();
            var settings = new SystemSettings();
            LicenseCheckResult? result = null;

            var licenseRepository = context.ServiceProvider
                .GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<LicenseInfo, Guid>>();
            LicenseInfo? existingLicense = null;
            try
            {
                var queryable = await licenseRepository.GetQueryableAsync();
                existingLicense = await queryable.FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to query LicenseInfo for LatestJwtToken");
            }

            if (existingLicense != null && !string.IsNullOrWhiteSpace(existingLicense.LatestJwtToken))
            {
                result = await licenseChecker.CheckLicenseFromTokenAsync(existingLicense.LatestJwtToken);
                if (result.IsSuccess)
                {
                    logger?.LogInformation("Startup license check from LatestJwtToken: Passed");
                }
                else
                {
                    logger?.LogWarning(
                        "Startup license check from LatestJwtToken failed: {Reason}, falling back to license file",
                        result.Message);
                    result = null;
                }
            }

            if (result == null)
            {
                result = await licenseChecker.CheckLicenseAsync(settings.LicenseFilePath);
                if (result.IsSuccess)
                {
                    logger?.LogInformation("Startup license check from license file: Passed");
                }
            }

            logger?.LogInformation(
                "Static license check: {Status} - {Message}",
                result.IsSuccess ? "Passed" : "Failed",
                result.Message);

            if (!result.IsSuccess || result.ProId == Guid.Empty)
            {
                var message = result.IsSuccess
                    ? "Authorization data is incomplete: missing project id."
                    : result.Message;
                return new UrbanStartupAuthorizationResult(false, message, null);
            }

            try
            {
                var uowManager = context.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

                using var uow = uowManager.Begin(true, false);
                var queryable2 = await licenseRepository.GetQueryableAsync();
                var existing = await queryable2.FirstOrDefaultAsync();

                if (existing == null)
                {
                    var machineCode = context.ServiceProvider.GetRequiredService<IMachineCodeService>().GetMachineCode();
                    var licenseInfo = new LicenseInfo(
                        Guid.NewGuid(),
                        result.ProId,
                        null,
                        result.AuthEndTime,
                        machineCode,
                        result.ProName,
                        result.BuildLicenseNo,
                        result.FdBuildLicenseNo);
                    await licenseRepository.InsertAsync(licenseInfo);
                }
                else
                {
                    existing.ProjectId = result.ProId;
                    existing.AuthEndTime = result.AuthEndTime;
                    var machineCode = context.ServiceProvider.GetRequiredService<IMachineCodeService>().GetMachineCode();
                    existing.Update(
                        null,
                        result.AuthEndTime,
                        machineCode,
                        result.ProName,
                        result.BuildLicenseNo,
                        result.FdBuildLicenseNo);
                    await licenseRepository.UpdateAsync(existing);
                }

                await uow.CompleteAsync();
                logger?.LogInformation(
                    "Static license data written to LicenseInfo: ProId={ProId}, ProName={ProName}",
                    result.ProId,
                    result.ProName);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to write static license data to LicenseInfo");
                return new UrbanStartupAuthorizationResult(
                    false,
                    "Failed to persist authorization data.",
                    null);
            }

            return new UrbanStartupAuthorizationResult(true, result.Message, result.ProId);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Static license check failed");
            return new UrbanStartupAuthorizationResult(false, ex.Message, null);
        }
    }

    public override async Task OnApplicationShutdownAsync(ApplicationShutdownContext context)
    {
        // Stop SignalR client gracefully
        try
        {
            var signalRClient = context.ServiceProvider.GetService<IDeviceStatusSignalRClient>();
            if (signalRClient != null)
            {
                await signalRClient.StopAsync();
            }
        }
        catch (Exception ex)
        {
            // Non-blocking: do not prevent shutdown
        }

        // Dispose SignalR client
        if (context.ServiceProvider.GetService<IDeviceStatusSignalRClient>() is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }

        // Flush and close Serilog
        await Log.CloseAndFlushAsync();
        await base.OnApplicationShutdownAsync(context);
    }

    private static async Task EnsureUrbanDefaultWeighingModeAsync(
        IServiceProvider serviceProvider,
        ILogger<MaterialClientUrbanModule>? logger)
    {
        try
        {
            var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
            var settings = await settingsService.GetSettingsAsync();
            var systemSettings = settings.SystemSettings;

            var changed = false;
            if (systemSettings.DefaultWeighingMode != WeighingMode.UrbanMode)
            {
                systemSettings.DefaultWeighingMode = WeighingMode.UrbanMode;
                changed = true;
            }

            if (!systemSettings.IsUrbanMode)
            {
                systemSettings.IsUrbanMode = true;
                changed = true;
            }

            if (!changed)
            {
                logger?.LogInformation("Urban default weighing mode already configured.");
                return;
            }

            settings.SystemSettings = systemSettings;
            await settingsService.SaveSettingsAsync(settings);

            logger?.LogInformation("Urban default weighing mode set to {Mode} (IsUrbanMode={IsUrbanMode}).",
                systemSettings.DefaultWeighingMode,
                systemSettings.IsUrbanMode);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to ensure Urban default weighing mode; startup will continue.");
        }
    }
}
}
