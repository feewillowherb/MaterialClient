using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

/// <summary>
/// 偏移结果类型
/// </summary>
public enum OffsetResultType : short
{
    /// <summary>
    /// 默认
    /// </summary>
    [Description("默认")]
    Default = 0,

    /// <summary>
    /// 超正差
    /// </summary>
    [Description("超正差")]
    OverPositiveDeviation = 1,

    /// <summary>
    /// 正常
    /// </summary>
    [Description("正常")]
    Normal = 2,

    /// <summary>
    /// 超负差
    /// </summary>
    [Description("超负差")]
    OverNegativeDeviation = 3
}

