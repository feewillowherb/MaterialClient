using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Events;

/// <summary>
///     道闸 IO 状态变更消息（通过 ReactiveUI MessageBus 发送）
/// </summary>
public class GateIOStateChangedMessage
{
    /// <summary>
    ///     变更前的状态
    /// </summary>
    public GateIOState PreviousState { get; set; }

    /// <summary>
    ///     变更后的当前状态
    /// </summary>
    public GateIOState CurrentState { get; set; }

    /// <summary>
    ///     状态变更时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    ///     状态变更原因（如 "识别事件触发"、"地磅稳定"、"超时"等）
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    public GateIOStateChangedMessage()
    {
    }

    public GateIOStateChangedMessage(GateIOState previousState, GateIOState currentState, string reason)
    {
        PreviousState = previousState;
        CurrentState = currentState;
        Reason = reason;
    }
}
