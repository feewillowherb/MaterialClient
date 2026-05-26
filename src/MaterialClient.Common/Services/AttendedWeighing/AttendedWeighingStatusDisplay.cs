using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Services.AttendedWeighing;

/// <summary>
///     Display text for <see cref="AttendedWeighingStatus" /> in weighing area UI.
/// </summary>
public static class AttendedWeighingStatusDisplay
{
    public static string GetStatusText(AttendedWeighingStatus status) =>
        status switch
        {
            AttendedWeighingStatus.OffScale => "称重已结束",
            AttendedWeighingStatus.WaitingForStability => "等待稳定",
            AttendedWeighingStatus.WeightStabilized => "重量已稳定",
            AttendedWeighingStatus.WaitingForDeparture => "等待下磅",
            _ => "未知状态"
        };
}
