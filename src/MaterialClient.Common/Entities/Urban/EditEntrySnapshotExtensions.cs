using MaterialClient.Common.Utils;

namespace MaterialClient.Common.Entities.Urban;

/// <summary>
///     <see cref="EditEntrySnapshot" /> 领域扩展：统一以千克（kg）为存储单位，
///     客户端称重字段以吨（t）输入时在此换算。
/// </summary>
public static class EditEntrySnapshotExtensions
{
    /// <summary>
    ///     从客户端称重记录字段创建快照。<paramref name="totalWeightInTon" /> 为吨，快照内 <see cref="EditEntrySnapshot.TotalWeight" /> 为千克。
    /// </summary>
    public static EditEntrySnapshot FromClientWeighing(
        string plateNumber,
        decimal totalWeightInTon,
        string? anomalyReason) =>
        new()
        {
            PlateNumber = plateNumber ?? string.Empty,
            TotalWeight = MaterialMath.ConvertTonToKg(totalWeightInTon),
            AnomalyReason = anomalyReason
        };
}
