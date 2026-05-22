namespace MaterialClient.Common.Events;

/// <summary>
///     称重记录创建事件数据（通过 ABP ILocalEventBus 发送）
/// </summary>
public class WeighingRecordCreatedEventData
{
    public WeighingRecordCreatedEventData(long weighingRecordId)
    {
        WeighingRecordId = weighingRecordId;
    }

    /// <summary>
    ///     新创建的称重记录ID
    /// </summary>
    public long WeighingRecordId { get; }
}
