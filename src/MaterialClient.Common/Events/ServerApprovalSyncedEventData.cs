namespace MaterialClient.Common.Events;

/// <summary>
///     Published after MaterialClient applies a server Web approval sync locally.
/// </summary>
public class ServerApprovalSyncedEventData
{
    public ServerApprovalSyncedEventData(long weighingRecordId)
    {
        WeighingRecordId = weighingRecordId;
    }

    public long WeighingRecordId { get; }
}
