using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Events;

/// <summary>
///     状态变化消息（用于 ReactiveUI MessageBus）
/// </summary>
public class StatusChangedMessage(AttendedWeighingStatus status)
{
    /// <summary>
    ///     新的称重状态
    /// </summary>
    public AttendedWeighingStatus Status { get; } = status;
}

