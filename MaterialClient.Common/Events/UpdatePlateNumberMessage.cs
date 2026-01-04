namespace MaterialClient.Common.Events;

/// <summary>
///     更新车牌号消息（用于 ReactiveUI MessageBus）
/// </summary>
public class UpdatePlateNumberMessage(long weighingRecordId, string? plateNumber)
{
    /// <summary>
    ///     称重记录ID
    /// </summary>
    public long WeighingRecordId { get; } = weighingRecordId;

    /// <summary>
    ///     新的车牌号
    /// </summary>
    public string? PlateNumber { get; } = plateNumber;
}

