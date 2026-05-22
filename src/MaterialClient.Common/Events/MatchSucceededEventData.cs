namespace MaterialClient.Common.Events;

/// <summary>
///     匹配成功事件数据（通过 ABP ILocalEventBus 发送）
/// </summary>
public class MatchSucceededEventData
{
    public MatchSucceededEventData(long waybillId, long weighingRecordId)
    {
        WaybillId = waybillId;
        WeighingRecordId = weighingRecordId;
    }

    /// <summary>
    ///     匹配成功后的运单ID
    /// </summary>
    public long WaybillId { get; }

    /// <summary>
    ///     触发匹配的称重记录ID
    /// </summary>
    public long WeighingRecordId { get; }
}
