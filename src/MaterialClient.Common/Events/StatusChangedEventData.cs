using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Events;

/// <summary>
///     称重状态变化事件数据（通过 ABP ILocalEventBus 发送）
/// </summary>
public class StatusChangedEventData
{
    public StatusChangedEventData(AttendedWeighingStatus status)
    {
        Status = status;
    }

    /// <summary>
    ///     新的称重状态
    /// </summary>
    public AttendedWeighingStatus Status { get; }
}
