using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.Vzvision;

namespace MaterialClient.Common.Events;

/// <summary>
///     车牌识别事件数据（通过 ABP ILocalEventBus 发送）
/// </summary>
public class LicensePlateRecognizedEventData
{
    /// <summary>
    ///     识别的车牌号
    /// </summary>
    public string PlateNumber { get; set; } = string.Empty;

    /// <summary>
    ///     可选的车牌颜色类型
    /// </summary>
    public VzvisionColorType? ColorType { get; set; }

    /// <summary>
    ///     识别车牌的设备类型
    /// </summary>
    public LprDeviceType DeviceType { get; set; }

    /// <summary>
    ///     人类可读的设备名称
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    ///     识别发生时的时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
