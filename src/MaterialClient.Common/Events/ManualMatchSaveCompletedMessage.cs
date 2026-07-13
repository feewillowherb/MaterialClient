namespace MaterialClient.Common.Events;

/// <summary>
///     Message sent when a manual match save operation completes.
///     Replaces the ManualMatchSaveCompleted EventHandler event and ManualMatchSaveCompletedEventArgs.
/// </summary>
public class ManualMatchSaveCompletedMessage(long? waybillId)
{
    /// <summary>
    ///     The waybill ID created by the manual match save operation
    /// </summary>
    public long? WaybillId { get; } = waybillId;
}
