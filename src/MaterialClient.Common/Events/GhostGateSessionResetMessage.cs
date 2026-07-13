namespace MaterialClient.Common.Events;

/// <summary>
///     道闸侧已判定幽灵会话并重置：旧会话车牌作废，供称重侧同步 <c>_plateNumberCache</c> / <c>LockedAt</c>。
/// </summary>
public class GhostGateSessionResetMessage(
    string abandonedPlateNumber,
    string? newPlateNumber = null,
    string? deviceName = null,
    DateTime? occurredAtUtc = null)
{
    /// <summary>
    ///     重置前会话绑定的车牌（与 <c>Reset()</c> 前会话一致）。
    /// </summary>
    public string AbandonedPlateNumber { get; } = abandonedPlateNumber;

    /// <summary>
    ///     触发重置的当前 LRP 车牌（新车牌）。
    /// </summary>
    public string? NewPlateNumber { get; } = newPlateNumber;

    /// <summary>
    ///     识别设备名称。
    /// </summary>
    public string? DeviceName { get; } = deviceName;

    /// <summary>
    ///     事件发生时间（UTC）。
    /// </summary>
    public DateTime OccurredAtUtc { get; } = occurredAtUtc ?? DateTime.UtcNow;
}
