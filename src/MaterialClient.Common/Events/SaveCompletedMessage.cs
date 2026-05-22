using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Events;

/// <summary>
///     保存完成消息（用于 ReactiveUI MessageBus）
/// </summary>
public class SaveCompletedMessage(long itemId, WeighingListItemType itemType)
{
    /// <summary>
    ///     保存的项ID
    /// </summary>
    public long ItemId { get; } = itemId;

    /// <summary>
    ///     项类型（WeighingRecord 或 Waybill）
    /// </summary>
    public WeighingListItemType ItemType { get; } = itemType;
}

