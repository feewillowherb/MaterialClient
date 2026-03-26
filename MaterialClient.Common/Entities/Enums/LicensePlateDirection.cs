using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

/// <summary>
///     车牌识别设备物理侧别枚举
///     A/B 表示物理位置侧别（入口/出口），与会话运行时角色（Entry/Exit）区分
/// </summary>
public enum LicensePlateDirection
{
    /// <summary>
    ///     物理侧别 A（入口位置）
    /// </summary>
    [Description("入口")]
    A = 0,

    /// <summary>
    ///     物理侧别 B（出口位置）
    /// </summary>
    [Description("出口")]
    B = 1
}