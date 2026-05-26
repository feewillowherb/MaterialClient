using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;

namespace MaterialClient.Common.Services;

/// <summary>
///     Urban anomaly detection service implementation.
///     Rules:
///     1. Plate number is null/empty/whitespace → anomaly
///     2. TotalWeight exceeds UpperLimit by DeviationPercentage → anomaly
///     3. TotalWeight is below LowerLimit by DeviationPercentage → anomaly
/// </summary>
public class UrbanAnomalyDetector : IUrbanAnomalyDetector
{
    /// <inheritdoc />
    public bool IsAnomaly(WeighingRecord record, UrbanAnomalyDetectionConfig config)
    {
        // Rule 1: Plate number is empty
        if (string.IsNullOrWhiteSpace(record.PlateNumber))
            return true;

        // Rule 2: Weight exceeds upper limit with deviation
        var upperThreshold = config.UpperLimit * (1 + config.DeviationPercentage / 100m);
        if (record.TotalWeight > upperThreshold)
            return true;

        // Rule 3: Weight is below lower limit with deviation
        var lowerThreshold = config.LowerLimit * (1 - config.DeviationPercentage / 100m);
        if (record.TotalWeight < lowerThreshold)
            return true;

        return false;
    }
}
