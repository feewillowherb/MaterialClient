namespace MaterialClient.Common.Events;

/// <summary>
///     物料同步完成消息（用于 ReactiveUI MessageBus）
/// </summary>
public class MaterialSyncedMessage(IReadOnlyList<int> syncedEntityIds)
{
    /// <summary>
    ///     已同步的物料实体ID列表
    /// </summary>
    public IReadOnlyList<int> SyncedEntityIds { get; } = syncedEntityIds;
}
