using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

/// <summary>
///     Attended weighing status enum
/// </summary>
public enum AttendedWeighingStatus
{
    /// <summary>
    ///     Off scale
    /// </summary>
    [Description("称重已结束")]
    OffScale = 0,

    /// <summary>
    ///     On scale waiting for weight stability
    /// </summary>
    [Description("等待稳定")]
    WaitingForStability = 1,

    /// <summary>
    ///     Weight stabilized
    /// </summary>
    [Description("重量已稳定")]
    WeightStabilized = 2,

    /// <summary>
    ///     Waiting for departure after weighing completed
    /// </summary>
    [Description("等待下磅")]
    WaitingForDeparture = 3
}