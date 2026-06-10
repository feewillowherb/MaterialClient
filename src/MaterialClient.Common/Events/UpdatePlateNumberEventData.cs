namespace MaterialClient.Common.Events;

/// <summary>
///     更新车牌号事件数据（通过 ABP ILocalEventBus 发送）
/// </summary>
public class UpdatePlateNumberEventData
{
    public UpdatePlateNumberEventData(long weighingRecordId, string? plateNumber)
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
