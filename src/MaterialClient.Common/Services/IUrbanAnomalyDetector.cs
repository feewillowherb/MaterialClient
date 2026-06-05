using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;

namespace MaterialClient.Common.Services;

/// <summary>
///     Urban anomaly detection service interface.
///     Determines whether a weighing record represents anomalous data.
/// </summary>
public interface IUrbanAnomalyDetector
{
    /// <summary>
    ///     Determines whether the specified weighing record is anomalous based on configured rules.
    /// </summary>
    /// <param name="record">The weighing record to evaluate.</param>
    /// <param name="config">The anomaly detection configuration thresholds.</param>
    /// <returns><c>true</c> if the record is anomalous; otherwise, <c>false</c>.</returns>
    bool IsAnomaly(WeighingRecord record, UrbanAnomalyDetectionConfig config);

    /// <summary>
    ///     Returns a short, human-readable anomaly reason text for UI display.
    /// </summary>
    /// <param name="record">The weighing record to evaluate.</param>
    /// <param name="config">The anomaly detection configuration thresholds.</param>
    /// <returns>Short reason (e.g. "车牌为空", "超上限", "低下限"), or <c>null</c> when normal.</returns>
    string? GetAnomalyReason(WeighingRecord record, UrbanAnomalyDetectionConfig config);
}
