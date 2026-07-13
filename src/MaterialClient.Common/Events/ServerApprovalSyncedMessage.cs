namespace MaterialClient.Common.Events;

/// <summary>
///     服务端审批同步完成消息（用于 ReactiveUI MessageBus），由桥接器从 <see cref="ServerApprovalSyncedEventData"/> 转接。
/// </summary>
public class ServerApprovalSyncedMessage(long weighingRecordId)
{
    /// <summary>
    ///     已同步审批的称重记录ID
    /// </summary>
    public long WeighingRecordId { get; } = weighingRecordId;
}
