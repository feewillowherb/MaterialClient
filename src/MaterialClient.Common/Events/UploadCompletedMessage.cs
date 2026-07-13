namespace MaterialClient.Common.Events;

/// <summary>
///     上传完成消息（用于 ReactiveUI MessageBus），由桥接器从 <see cref="UploadCompletedEventData"/> 转接。
/// </summary>
public class UploadCompletedMessage(long weighingRecordId)
{
    /// <summary>
    ///     已上传的称重记录ID
    /// </summary>
    public long WeighingRecordId { get; } = weighingRecordId;
}
