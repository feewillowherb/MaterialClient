namespace MaterialClient.Common.Configuration;

/// <summary>
///     Weighing configuration class
/// </summary>
public class WeighingConfiguration
{
    /// <summary>
    ///     最小称重重量稳定判定（吨）
    /// </summary>
    public decimal MinWeightThreshold { get; set; } = 0.5m; // 0.5t = 500kg

    /// <summary>
    ///     重量稳定性阈值（吨）
    /// </summary>
    public decimal WeightStabilityThreshold { get; set; } = 0.05m; // ±0.05m = 0.1m total range

    /// <summary>
    ///     稳定性监控窗口时间（毫秒）
    /// </summary>
    public int StabilityWindowMs { get; set; } = 3000;

    /// <summary>
    ///     稳定性检查间隔（毫秒）
    /// </summary>
    public int StabilityCheckIntervalMs { get; set; } = 200; // 默认 200ms
}