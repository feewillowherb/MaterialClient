using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

/// <summary>
///     道闸进出口方向枚举
///     独立于 LPR 的 LicensePlateDirection，用于道闸 IO 控制领域
/// </summary>
public enum GateIODirection
{
    /// <summary>
    ///     进口道闸
    ///     映射关系：LicensePlateDirection.In → GateIODirection.Entry
    /// </summary>
    [Description("进口")]
    Entry = 0,

    /// <summary>
    ///     出口道闸
    ///     映射关系：LicensePlateDirection.Out → GateIODirection.Exit
    /// </summary>
    [Description("出口")]
    Exit = 1
}
