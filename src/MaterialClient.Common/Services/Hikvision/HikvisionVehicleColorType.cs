using System.ComponentModel;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     车身颜色（与海康 SDK 车辆抓拍信息一致）
/// </summary>
public enum HikvisionVehicleColorType
{
    [Description("未知")]
    Unknown = 0,

    [Description("白色")]
    White = 1,

    [Description("黑色")]
    Black = 2,

    [Description("灰色")]
    Gray = 3,

    [Description("银色")]
    Silver = 4,

    [Description("红色")]
    Red = 5,

    [Description("蓝色")]
    Blue = 6,

    [Description("黄色")]
    Yellow = 7,

    [Description("绿色")]
    Green = 8,

    [Description("棕色")]
    Brown = 9,

    [Description("其他")]
    Other = 99
}