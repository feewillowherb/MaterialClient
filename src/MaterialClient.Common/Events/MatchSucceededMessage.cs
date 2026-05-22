namespace MaterialClient.Common.Events;

/// <summary>
///     匹配成功消息（用于 ReactiveUI MessageBus）
/// </summary>
public class MatchSucceededMessage(long waybillId, long weighingRecordId)
{
    /// <summary>
    ///     匹配成功后的运单ID
    /// </summary>
    public long WaybillId { get; } = waybillId;

    /// <summary>
    ///     触发匹配的称重记录ID
    /// </summary>
    public long WeighingRecordId { get; } = weighingRecordId;
}