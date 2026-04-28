namespace MaterialClient.Common.Events;

/// <summary>
///     道闸侧已判定幽灵会话并重置事件数据（通过 ABP ILocalEventBus 发送）
/// </summary>
public class GhostGateSessionResetEventData
{
    public GhostGateSessionResetEventData(
        string abandonedPlateNumber,
        string? newPlateNumber = null,
        string? deviceName = null,
        DateTime? occurredAtUtc = null)
    {
        AbandonedPlateNumber = abandonedPlateNumber;
        NewPlateNumber = newPlateNumber;
        DeviceName = deviceName;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    ///     重置前会话绑定的车牌
    /// </summary>
    public string AbandonedPlateNumber { get; }

    /// <summary>
    ///     触发重置的当前 LRP 车牌（新车牌）
    /// </summary>
    public string? NewPlateNumber { get; }

    /// <summary>
    ///     识别设备名称
    /// </summary>
    public string? DeviceName { get; }

    /// <summary>
    ///     事件发生时间（UTC）
    /// </summary>
    public DateTime OccurredAtUtc { get; }
}
