namespace MaterialClient.Common.Entities.Enums;

/// <summary>
///     Attended weighing status enum
/// </summary>
public enum AttendedWeighingStatus
{
    /// <summary>
    ///     Off scale
    /// </summary>
    OffScale = 0,

    /// <summary>
    ///     On scale waiting for weight stability
    /// </summary>
    WaitingForStability = 1,

    /// <summary>
    ///     Weight stabilized
    /// </summary>
    WeightStabilized = 2,

    /// <summary>
    ///     Waiting for departure after weighing completed
    /// </summary>
    WaitingForDeparture = 3
}