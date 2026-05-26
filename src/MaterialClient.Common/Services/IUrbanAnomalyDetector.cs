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
}
