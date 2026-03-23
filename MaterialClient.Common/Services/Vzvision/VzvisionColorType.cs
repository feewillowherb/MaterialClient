using System.ComponentModel;

namespace MaterialClient.Common.Services.Vzvision;

/// <summary>
///     车牌颜色（与 Vz SDK LC_* / <c>nColor</c> 一致）
/// </summary>
public enum VzvisionColorType
{
    [Description("未知")]
    Unknown = 0,

    [Description("蓝色")]
    Blue = 1,

    [Description("黄色")]
    Yellow = 2,

    [Description("白色")]
    White = 3,

    [Description("黑色")]
    Black = 4,

    [Description("绿色")]
    Green = 5
}
