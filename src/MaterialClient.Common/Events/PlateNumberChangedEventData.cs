namespace MaterialClient.Common.Events;

/// <summary>
///     车牌号变化事件数据（通过 ABP ILocalEventBus 发送）
/// </summary>
public class PlateNumberChangedEventData
{
    public PlateNumberChangedEventData(string? plateNumber)
    {
        PlateNumber = plateNumber;
    }

    /// <summary>
    ///     新的车牌号（null 表示已清空）
    /// </summary>
    public string? PlateNumber { get; }
}
