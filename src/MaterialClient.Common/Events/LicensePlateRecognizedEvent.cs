using System;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Events;

/// <summary>
///     车牌识别事件(临时定义,后续提案完善)
/// </summary>
public class LicensePlateRecognizedEvent
{
    /// <summary>
    ///     车牌号
    /// </summary>
    public string PlateNumber { get; set; } = string.Empty;

    /// <summary>
    ///     进出方向
    /// </summary>
    public LicensePlateDirection Direction { get; set; }

    /// <summary>
    ///     识别时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    ///     设备名称
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    // 后续可能添加: 图片路径、置信度、车牌颜色等
}
