using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Events;

/// <summary>
///     保存完成事件数据（通过 ABP ILocalEventBus 发送）
/// </summary>
public class SaveCompletedEventData(long itemId, WeighingListItemType itemType)
{
    public long ItemId { get; } = itemId;

    public WeighingListItemType ItemType { get; } = itemType;
}
