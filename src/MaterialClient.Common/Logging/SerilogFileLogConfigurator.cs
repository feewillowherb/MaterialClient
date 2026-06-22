using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.File;

namespace MaterialClient.Common.Logging;

/// <summary>
/// Configures Serilog file logging with optional date-based subdirectories.
/// </summary>
public static class SerilogFileLogConfigurator
{
    public static void Configure(IServiceCollection services, IConfiguration configuration, string fileNamePrefix)
    {
        var logEnabled = configuration.GetValue("Log:Enabled", true);
        if (!logEnabled)
        {
            services.AddLogging(logging => logging.ClearProviders());
            return;
        }

        var appDirectory = AppContext.BaseDirectory;
        var logDirectory = configuration.GetValue<string>("Log:Directory", "Logs");
        var logsDirectory = Path.IsPathRooted(logDirectory)
            ? logDirectory
            : Path.Combine(appDirectory, logDirectory);

        Directory.CreateDirectory(logsDirectory);

        var useDateFolders = configuration.GetValue("Log:UseDateFolders", true);
        var defaultLevel = GetLogLevel(configuration, "Logging:LogLevel:Default", "Information");
        var microsoftLevel = GetLogLevel(configuration, "Logging:LogLevel:Microsoft", "Warning");
        var efCoreLevel = GetLogLevel(configuration, "Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
        var abpLevel = GetLogLevel(configuration, "Logging:LogLevel:Volo.Abp", "Warning");

        var fileSizeLimitMB = configuration.GetValue("Log:FileSizeLimitMB", 50);
        var retentionDays = configuration.GetValue("Log:RetentionDays", 30);

        if (fileSizeLimitMB is < 10 or > 500)
        {
            Log.Warning("Invalid Log:FileSizeLimitMB value: {FileSizeLimitMB}. Using default 50MB.", fileSizeLimitMB);
            fileSizeLimitMB = 50;
        }

        var fileSizeLimitBytes = fileSizeLimitMB * 1024L * 1024L;
        var retainedFileTimeLimit = retentionDays > 0 ? TimeSpan.FromDays(retentionDays) : (TimeSpan?)null;
        const string outputTemplate =
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        var loggerConfig = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Is(ParseLogEventLevel(defaultLevel))
            .MinimumLevel.Override("Microsoft", ParseLogEventLevel(microsoftLevel))
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", ParseLogEventLevel(efCoreLevel))
            .MinimumLevel.Override("Volo.Abp", ParseLogEventLevel(abpLevel));

        if (useDateFolders)
        {
            loggerConfig.WriteTo.Map(
                le => new DateTime(le.Timestamp.Year, le.Timestamp.Month, le.Timestamp.Day),
                (date, wt) => wt.File(
                    Path.Combine(
                        logsDirectory,
                        date.ToString("yyyy"),
                        date.ToString("MM"),
                        date.ToString("dd"),
                        $"{fileNamePrefix}-.log"),
                    rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: fileSizeLimitBytes,
                    retainedFileTimeLimit: retainedFileTimeLimit,
                    outputTemplate: outputTemplate,
                    encoding: Encoding.UTF8),
                sinkMapCountLimit: 1);
        }
        else
        {
            loggerConfig.WriteTo.File(
                Path.Combine(logsDirectory, $"{fileNamePrefix}-.log"),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: fileSizeLimitBytes,
                retainedFileTimeLimit: retainedFileTimeLimit,
                outputTemplate: outputTemplate,
                encoding: Encoding.UTF8);
        }

        Log.Logger = loggerConfig.CreateLogger();

        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSerilog(Log.Logger);
        });
    }

    private static string GetLogLevel(IConfiguration configuration, string key, string defaultValue)
        => configuration[key] ?? defaultValue;

    private static LogEventLevel ParseLogEventLevel(string level)
        => Enum.TryParse<LogEventLevel>(level, true, out var result)
            ? result
            : LogEventLevel.Information;
}
