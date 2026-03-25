using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.Vzvision;

namespace MaterialClient.Common.Events;

/// <summary>
///     车牌识别消息(通过 ReactiveUI MessageBus 发送)
/// </summary>
/// <remarks>
///     此消息由 LPR 硬件设备识别车牌时发布,用于解耦硬件集成层与业务逻辑层。
///     通过 MessageBus 发布,符合 ADR-009 架构决策。
/// </remarks>
public class LicensePlateRecognizedMessage
{
    /// <summary>
    ///     识别的车牌号(例如 "京A12345")
    /// </summary>
    public string PlateNumber { get; set; } = string.Empty;

    /// <summary>
    ///     可选的车牌颜色类型(例如 蓝色、黄色、绿色)
    /// </summary>
    public VzvisionColorType? ColorType { get; set; }

    /// <summary>
    ///     识别车牌的设备类型
    /// </summary>
    public LprDeviceType DeviceType { get; set; }

    /// <summary>
    ///     人类可读的设备名称(来自配置)
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    ///     识别方向（进口/出口，来自配置）
    /// </summary>
    public LicensePlateDirection Direction { get; set; } = LicensePlateDirection.In;

    /// <summary>
    ///     识别发生时的时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
