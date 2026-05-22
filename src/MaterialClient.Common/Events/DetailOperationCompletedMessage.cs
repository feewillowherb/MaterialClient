using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Events;

/// <summary>
///     Message sent when a detail operation (Save, Abolish, Match, Complete) completes.
///     Replaces individual EventHandler&lt;ItemOperationCompletedEventArgs&gt; events.
/// </summary>
public class DetailOperationCompletedMessage(
    long itemId,
    WeighingListItemType itemType,
    OrderTypeEnum? orderType,
    bool isCompleted,
    DetailOperationType operationType)
{
    /// <summary>
    ///     Operation target item ID
    /// </summary>
    public long ItemId { get; } = itemId;

    /// <summary>
    ///     Item type (WeighingRecord or Waybill)
    /// </summary>
    public WeighingListItemType ItemType { get; } = itemType;

    /// <summary>
    ///     Order type (Unmatch, FirstWeight, Completed)
    /// </summary>
    public OrderTypeEnum? OrderType { get; } = orderType;

    /// <summary>
    ///     Whether the item is completed
    /// </summary>
    public bool IsCompleted { get; } = isCompleted;

    /// <summary>
    ///     Operation type that triggered this message
    /// </summary>
    public DetailOperationType OperationType { get; } = operationType;
}
