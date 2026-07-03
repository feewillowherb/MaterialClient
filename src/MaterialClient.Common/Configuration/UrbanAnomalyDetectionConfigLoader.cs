using MaterialClient.Common.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Configuration;

/// <summary>
///     Loads Urban anomaly detection thresholds from persisted settings,
///     with optional appsettings fallback when settings are unavailable.
/// </summary>
public static class UrbanAnomalyDetectionConfigLoader
{
    public static async Task<UrbanAnomalyDetectionConfig> LoadAsync(
        ISettingsService settingsService,
        IConfiguration? configuration = null,
        ILogger? logger = null)
    {
        try
        {
            var settings = await settingsService.GetSettingsAsync();
            return settings.SystemSettings.UrbanAnomalyDetection ?? new UrbanAnomalyDetectionConfig();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex,
                "Failed to read UrbanAnomalyDetection from settings, falling back to appsettings defaults");

            return LoadFromConfiguration(configuration, logger);
        }
    }

    private static UrbanAnomalyDetectionConfig LoadFromConfiguration(
        IConfiguration? configuration,
        ILogger? logger)
    {
        if (configuration == null)
        {
            return new UrbanAnomalyDetectionConfig();
        }

        try
        {
            var config = new UrbanAnomalyDetectionConfig();
            configuration.GetSection("UrbanAnomalyDetection").Bind(config);
            return config;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to read UrbanAnomalyDetection from appsettings, using default values");
            return new UrbanAnomalyDetectionConfig();
        }
    }
}
