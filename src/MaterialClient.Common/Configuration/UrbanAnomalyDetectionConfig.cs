namespace MaterialClient.Common.Configuration;

/// <summary>
///     Urban anomaly detection configuration model.
///     Primary source: <see cref="SystemSettings.UrbanAnomalyDetection" /> via <see cref="Services.ISettingsService" />.
///     Fallback: "UrbanAnomalyDetection" section in appsettings.json when settings cannot be loaded.
/// </summary>
public class UrbanAnomalyDetectionConfig
{
    /// <summary>
    ///     Upper weight limit (in tons). Weights exceeding this by the deviation percentage are anomalies.
    /// </summary>
    public decimal UpperLimit { get; set; } = 50.0m;

    /// <summary>
    ///     Lower weight limit (in tons). Weights below this by the deviation percentage are anomalies.
    /// </summary>
    public decimal LowerLimit { get; set; } = 1.0m;

    /// <summary>
    ///     Allowed deviation percentage for weight thresholds.
    /// </summary>
    public decimal DeviationPercentage { get; set; } = 10.0m;
}
