using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;

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
    /// <param name="hasLprAttachment"><c>true</c> if an Lpr photo attachment exists for this record.</param>
    /// <returns><c>true</c> if the record is anomalous; otherwise, <c>false</c>.</returns>
    bool IsAnomaly(WeighingRecord record, UrbanAnomalyDetectionConfig config, bool hasLprAttachment = true);

    /// <summary>
    ///     Returns the anomaly reason enum for the detected anomaly.
    /// </summary>
    /// <param name="record">The weighing record to evaluate.</param>
    /// <param name="config">The anomaly detection configuration thresholds.</param>
    /// <param name="hasLprAttachment"><c>true</c> if an Lpr photo attachment exists for this record.</param>
    /// <returns>Anomaly reason enum, or <c>null</c> when normal.</returns>
    AnomalyReason? GetAnomalyReason(WeighingRecord record, UrbanAnomalyDetectionConfig config, bool hasLprAttachment = true);
}
