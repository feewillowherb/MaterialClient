using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

/// <summary>
///     城管称重记录异常原因
/// </summary>
public enum AnomalyReason
{
    [Description("抓拍异常")]
    CaptureFailure = 1,

    [Description("车牌为空")]
    EmptyPlate = 2,

    [Description("超上限")]
    OverUpperLimit = 3,

    [Description("低下限")]
    UnderLowerLimit = 4
}
