using System;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.Vzvision;

namespace MaterialClient.Common.Events;

/// <summary>
///     兼容旧测试的车牌识别消息类型。
/// </summary>
public class LicensePlateRecognizedMessage
{
    public string? PlateNumber { get; set; }
    public VzvisionColorType? ColorType { get; set; }
    public string? VehicleColor { get; set; }
    public string? VehicleType { get; set; }
    public string? PlateColor { get; set; }
    public LprDeviceType DeviceType { get; set; }
    public string? DeviceName { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
