namespace MaterialClient.Common.Events;

/// <summary>
///     供应商同步完成消息（用于 ReactiveUI MessageBus）
/// </summary>
public class ProviderSyncedMessage(IReadOnlyList<int> syncedEntityIds)
{
    /// <summary>
    ///     已同步的供应商实体ID列表
    /// </summary>
    public IReadOnlyList<int> SyncedEntityIds { get; } = syncedEntityIds;
}
