using System.ComponentModel;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     车牌颜色（与海康 SDK 车牌抓拍信息一致）
/// </summary>
public enum HikvisionPlateColorType
{
    [Description("未知")]
    Unknown = 0,

    [Description("蓝色")]
    Blue = 1,

    [Description("黄色")]
    Yellow = 2,

    [Description("黑色")]
    Black = 3,

    [Description("白色")]
    White = 4,

    [Description("绿色")]
    Green = 5,

    [Description("其他")]
    Other = 99
}