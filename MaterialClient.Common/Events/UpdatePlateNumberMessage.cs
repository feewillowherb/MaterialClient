namespace MaterialClient.Common.Events;

/// <summary>
///     更新车牌号消息（用于 ReactiveUI MessageBus）
/// </summary>
public class UpdatePlateNumberMessage
{
    public UpdatePlateNumberMessage(long weighingRecordId, string? plateNumber)
    {
        WeighingRecordId = weighingRecordId;
        PlateNumber = plateNumber;
    }

    /// <summary>
    ///     称重记录ID
    /// </summary>
    public long WeighingRecordId { get; }

    /// <summary>
    ///     新的车牌号
    /// </summary>
    public string? PlateNumber { get; }
}

