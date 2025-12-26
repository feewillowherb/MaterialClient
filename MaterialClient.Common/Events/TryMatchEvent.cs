namespace MaterialClient.Common.Events;

/// <summary>
///     尝试匹配称重记录的本地事件
/// </summary>
public class TryMatchEvent
{
    public TryMatchEvent(long weighingRecordId)
    {
        WeighingRecordId = weighingRecordId;
    }

    /// <summary>
    ///     称重记录ID
    /// </summary>
    public long WeighingRecordId { get; set; }
}