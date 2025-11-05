using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

/// <summary>
/// 订单来源
/// </summary>
public enum OrderSource : short
{
    /// <summary>
    /// 有人值守
    /// </summary>
    [Description("有人值守")]
    MannedStation = 1,

    /// <summary>
    /// 补录
    /// </summary>
    [Description("补录")]
    ManualEntry = 2,

    /// <summary>
    /// 移动验收
    /// </summary>
    [Description("移动验收")]
    MobileAcceptance = 3,

    /// <summary>
    /// 无人值守
    /// </summary>
    [Description("无人值守")]
    UnmannedStation = 4
}

