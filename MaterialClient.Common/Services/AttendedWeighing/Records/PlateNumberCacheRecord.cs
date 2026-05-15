using MaterialClient.Common.Services.Vzvision;

namespace MaterialClient.Common.Services.AttendedWeighing.Records;

/// <summary>
///     车牌缓存记录
/// </summary>
public record PlateNumberCacheRecord
{
    /// <summary>
    ///     识别次数
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    ///     最后更新时间
    /// </summary>
    public DateTime LastUpdateTime { get; init; }

    /// <summary>
    ///     车牌颜色类型（用于优先级判断）
    /// </summary>
    public VzvisionColorType? ColorType { get; init; }

    /// <summary>
    ///     锁定时间（用于关闭车牌重写时的周期内稳定选择）
    /// </summary>
    public DateTime? LockedAt { get; init; }
}
