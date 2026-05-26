using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Dtos.Urban;

/// <summary>
///     Urban attended weighing list row for UI binding (no EF entities).
/// </summary>
public class UrbanWeighingListItemDto
{
    public long WeighingRecordId { get; init; }

    public string? PlateNumber { get; init; }

    public DateTime AddDate { get; init; }

    public decimal TotalWeight { get; init; }

    /// <summary>
    ///     Data-quality anomaly flag (tab filter and primary status badge).
    /// </summary>
    public bool IsAnomaly { get; init; }

    /// <summary>
    ///     Upload sync status; null when no extension row exists.
    /// </summary>
    public SyncStatus? SyncStatus { get; init; }
}
