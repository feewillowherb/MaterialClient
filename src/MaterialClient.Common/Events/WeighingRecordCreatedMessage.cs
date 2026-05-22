namespace MaterialClient.Common.Events;

/// <summary>
///     称重记录创建消息（用于 ReactiveUI MessageBus）
/// </summary>
public class WeighingRecordCreatedMessage(long weighingRecordId)
{
    /// <summary>
    ///     新创建的称重记录ID
    /// </summary>
    public long WeighingRecordId { get; } = weighingRecordId;
}

