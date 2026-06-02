using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Events;

/// <summary>
///     详情操作完成事件数据（通过 ABP ILocalEventBus 发送）
/// </summary>
public class DetailOperationCompletedEventData(
    long itemId,
    WeighingListItemType itemType,
    OrderTypeEnum? orderType,
    bool isCompleted,
    DetailOperationType operationType)
{
    public long ItemId { get; } = itemId;

    public WeighingListItemType ItemType { get; } = itemType;

    public OrderTypeEnum? OrderType { get; } = orderType;

    public bool IsCompleted { get; } = isCompleted;

    public DetailOperationType OperationType { get; } = operationType;
}
