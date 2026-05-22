namespace MaterialClient.Common.Events;

/// <summary>
///     车牌号变化消息（用于 ReactiveUI MessageBus）
/// </summary>
public class PlateNumberChangedMessage(string? plateNumber)
{
    /// <summary>
    ///     新的车牌号（null 表示已清空）
    /// </summary>
    public string? PlateNumber { get; } = plateNumber;
}

