namespace MaterialClient.Common.Services.AttendedWeighing.Records;

/// <summary>
///     重量稳定性信息
/// </summary>
public record WeightStabilityInfo
{
    /// <summary>
    ///     当前重量（窗口内最新值）
    /// </summary>
    public decimal Weight { get; init; }

    /// <summary>
    ///     是否稳定
    /// </summary>
    public bool IsStable { get; init; }

    /// <summary>
    ///     稳定值（稳定时为平均值，否则为null）
    /// </summary>
    public decimal? StableWeight { get; init; }

    /// <summary>
    ///     最小值
    /// </summary>
    public decimal Min { get; init; }

    /// <summary>
    ///     最大值
    /// </summary>
    public decimal Max { get; init; }

    /// <summary>
    ///     范围
    /// </summary>
    public decimal Range { get; init; }
}
