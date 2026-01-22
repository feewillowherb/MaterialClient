using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Events;

/// <summary>
///     事件参数：操作完成后的项目上下文信息
/// </summary>
public class ItemOperationCompletedEventArgs : EventArgs
{
    public ItemOperationCompletedEventArgs(
        long itemId,
        WeighingListItemType itemType,
        OrderTypeEnum? orderType,
        bool isCompleted,
        string operationType)
    {
        ItemId = itemId;
        ItemType = itemType;
        OrderType = orderType;
        IsCompleted = isCompleted;
        OperationType = operationType;
    }

    /// <summary>
    ///     操作后的项目ID
    /// </summary>
    public long ItemId { get; }

    /// <summary>
    ///     项目类型（WeighingRecord 或 Waybill）
    /// </summary>
    public WeighingListItemType ItemType { get; }

    /// <summary>
    ///     订单类型（Unmatch, FirstWeight, Completed）
    /// </summary>
    public OrderTypeEnum? OrderType { get; }

    /// <summary>
    ///     是否已完成（快速检查标志）
    /// </summary>
    public bool IsCompleted { get; }

    /// <summary>
    ///     操作类型（"Save", "Complete", "Match", "Abolish", "ManualMatch"）
    /// </summary>
    public string OperationType { get; }
}
