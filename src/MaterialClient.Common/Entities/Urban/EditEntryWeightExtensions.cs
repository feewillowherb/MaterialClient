using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Utils;

namespace MaterialClient.Common.Entities.Urban;

/// <summary>
///     <see cref="EditEntry" /> 重量单位领域扩展：上云前将修改历史中的重量规范为千克（kg）。
/// </summary>
public static class EditEntryWeightExtensions
{
    /// <summary>
    ///     卡车称重千克值通常 ≥ 1000；低于此阈值的客户端条目视为遗留的吨值。
    /// </summary>
    private const decimal ClientTonLikelyThresholdKg = 1000m;

    /// <summary>
    ///     将单条修改历史的快照重量规范为服务端存储单位（kg）。
    /// </summary>
    public static EditEntry NormalizeWeightsForServer(this EditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Source != EditSource.Client)
        {
            return entry;
        }

        return new EditEntry
        {
            ChangedAt = entry.ChangedAt,
            Before = entry.Before.NormalizeSnapshotWeightForServer(),
            After = entry.After.NormalizeSnapshotWeightForServer(),
            Source = entry.Source,
            IsImagesModified = entry.IsImagesModified
        };
    }

    /// <summary>
    ///     批量将修改历史重量规范为服务端存储单位（kg）。
    /// </summary>
    public static List<EditEntry> NormalizeWeightsForServer(this IEnumerable<EditEntry> entries) =>
        entries.Select(entry => entry.NormalizeWeightsForServer()).ToList();

    private static EditEntrySnapshot NormalizeSnapshotWeightForServer(this EditEntrySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var weight = snapshot.TotalWeight;
        if (weight <= 0 || weight >= ClientTonLikelyThresholdKg)
        {
            return snapshot;
        }

        return new EditEntrySnapshot
        {
            PlateNumber = snapshot.PlateNumber,
            TotalWeight = MaterialMath.ConvertTonToKg(weight),
            AnomalyReason = snapshot.AnomalyReason
        };
    }
}
