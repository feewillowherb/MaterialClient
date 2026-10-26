using MaterialClient.Common.Configuration;
using MaterialClient.Common.Services.AttendedWeighing.Records;

namespace MaterialClient.Common.Services.AttendedWeighing;

/// <summary>
///     Shared stability predicate for downsampled full-window evaluation
///     (WeighingStreamPipeline and AttendedWeighingService).
/// </summary>
internal static class WeightStabilityEvaluator
{
    /// <summary>
    ///     Number of StabilityCheckIntervalMs periods that must fill StabilityWindowMs.
    /// </summary>
    public static int ResolvePeriods(WeighingConfiguration config)
    {
        if (config.StabilityCheckIntervalMs <= 0)
        {
            return 1;
        }

        return Math.Max(1, config.StabilityWindowMs / config.StabilityCheckIntervalMs);
    }

    /// <summary>
    ///     Evaluate a ready downsampled window of exactly <paramref name="periods"/> samples.
    ///     Stable only when every sample is above MinWeightThreshold and range &lt;= threshold * 2.
    /// </summary>
    public static WeightStabilityInfo FromDownsampledWindow(
        IList<decimal> window,
        int periods,
        WeighingConfiguration config)
    {
        if (window.Count != periods)
        {
            return Unstable();
        }

        if (window.Any(w => w <= config.MinWeightThreshold))
        {
            return Unstable();
        }

        var min = window.Min();
        var max = window.Max();
        var range = max - min;
        var isStable = range <= config.WeightStabilityThreshold * 2;

        return new WeightStabilityInfo
        {
            Weight = window[^1],
            IsStable = isStable,
            StableWeight = isStable ? (min + max) / 2 : null,
            Min = min,
            Max = max,
            Range = range
        };
    }

    public static WeightStabilityInfo Unstable() => new()
    {
        Weight = 0m,
        IsStable = false,
        StableWeight = null,
        Min = 0m,
        Max = 0m,
        Range = 0m
    };
}
