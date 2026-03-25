using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Events;

/// <summary>
///     道闸 IO 状态详细信息
/// </summary>
public class GateIOStateDetails
{
    /// <summary>
    ///     当前状态
    /// </summary>
    public GateIOState State { get; set; }

    /// <summary>
    ///     锁定时记录的车辆进入方向（仅在 Locked 状态下有意义）
    /// </summary>
    public GateIODirection? LockedDirection { get; set; }

    /// <summary>
    ///     锁定持续时间（仅在 Locked 状态下有意义）
    /// </summary>
    public TimeSpan? LockedDuration { get; set; }

    /// <summary>
    ///     最近的错误消息（仅在 Error 状态下有意义）
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    ///     状态最后更新时间
    /// </summary>
    public DateTime LastStateUpdateTime { get; set; } = DateTime.Now;
}
