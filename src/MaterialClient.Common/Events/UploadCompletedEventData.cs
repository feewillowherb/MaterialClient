namespace MaterialClient.Common.Events;

/// <summary>
///     上传完成事件数据（通过 ABP ILocalEventBus 发送）
/// </summary>
public class UploadCompletedEventData
{
    public UploadCompletedEventData(long weighingRecordId)
    {
        WeighingRecordId = weighingRecordId;
    }

    /// <summary>
    ///     已上传的称重记录ID
    /// </summary>
    public long WeighingRecordId { get; }
}
