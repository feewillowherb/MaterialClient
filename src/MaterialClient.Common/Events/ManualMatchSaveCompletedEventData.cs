namespace MaterialClient.Common.Events;

/// <summary>
///     手动匹配保存完成事件数据（通过 ABP ILocalEventBus 发送）
/// </summary>
public class ManualMatchSaveCompletedEventData(long? waybillId)
{
    public long? WaybillId { get; } = waybillId;
}
