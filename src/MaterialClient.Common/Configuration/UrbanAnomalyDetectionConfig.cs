namespace MaterialClient.Common.Configuration;

/// <summary>
///     Urban anomaly detection configuration model.
///     Bound from the "UrbanAnomalyDetection" section in appsettings.json.
/// </summary>
public class UrbanAnomalyDetectionConfig
{
    /// <summary>
    ///     Upper weight limit (in tons). Weights exceeding this by the deviation percentage are anomalies.
    /// </summary>
    public decimal UpperLimit { get; set; } = 30.0m;

    /// <summary>
    ///     Lower weight limit (in tons). Weights below this by the deviation percentage are anomalies.
    /// </summary>
    public decimal LowerLimit { get; set; } = 2.0m;

    /// <summary>
    ///     Allowed deviation percentage for weight thresholds.
    /// </summary>
    public decimal DeviationPercentage { get; set; } = 10.0m;
}
