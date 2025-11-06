namespace MaterialClient.Common.Configuration;

/// <summary>
/// Weighing configuration class
/// </summary>
public class WeighingConfiguration
{
    /// <summary>
    /// 偏移范围下限
    /// </summary>
    public decimal WeightOffsetRangeMin { get; set; } = -1m;

    /// <summary>
    /// 偏移范围上限
    /// </summary>
    public decimal WeightOffsetRangeMax { get; set; } = 1m;

    /// <summary>
    /// 稳定时间（毫秒）
    /// </summary>
    public int WeightStableDurationMs { get; set; } = 2000;

    /// <summary>
    /// 匹配时间窗口（小时）
    /// </summary>
    public int WeighingMatchDurationHours { get; set; } = 3;
}

