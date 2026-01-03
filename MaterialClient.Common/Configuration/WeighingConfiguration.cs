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

    /// <summary>
    ///     匹配最大时间间隔（分钟）
    /// </summary>
    public int MaxIntervalMinutes { get; set; } = 300;

    /// <summary>
    ///     匹配最小重量差（吨）
    /// </summary>
    public decimal MinWeightDiff { get; set; } = 1m;

    /// <summary>
    ///     判断配置是否有效
    ///     验证所有数值参数是否在合理范围内
    /// </summary>
    /// <returns>如果配置有效返回true，否则返回false</returns>
    public bool IsValid()
    {
        return MinWeightThreshold > 0 &&
               WeightStabilityThreshold > 0 &&
               StabilityWindowMs > 0 &&
               StabilityCheckIntervalMs > 0 &&
               StabilityCheckIntervalMs <= StabilityWindowMs &&
               MaxIntervalMinutes > 0 &&
               MinWeightDiff > 0;
    }
}