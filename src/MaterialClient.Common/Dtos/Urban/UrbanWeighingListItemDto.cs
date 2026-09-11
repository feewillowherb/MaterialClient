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

    /// <summary>
    ///     Anomaly reason enum, null when record is normal.
    /// </summary>
    public AnomalyReason? AnomalyReason { get; init; }

    /// <summary>
    ///     Record upload time (when available), null means not uploaded yet.
    /// </summary>
    public DateTime? UploadTime { get; init; }

    /// <summary>
    ///     Scale LPR in/out when stored on the extension; null when unknown.
    /// </summary>
    public UrbanInOutType? UrbanInOutType { get; init; }

    public static UrbanWeighingListItemDto FromWeighingFields(
        long weighingRecordId,
        string? plateNumber,
        DateTime addDate,
        decimal totalWeight,
        bool isAnomaly,
        SyncStatus? syncStatus,
        AnomalyReason? anomalyReason,
        DateTime? uploadTime,
        UrbanInOutType? urbanInOutType)
    {
        return new UrbanWeighingListItemDto
        {
            WeighingRecordId = weighingRecordId,
            PlateNumber = plateNumber,
            AddDate = addDate,
            TotalWeight = totalWeight,
            IsAnomaly = isAnomaly,
            SyncStatus = syncStatus,
            AnomalyReason = anomalyReason,
            UploadTime = uploadTime,
            UrbanInOutType = urbanInOutType
        };
    }
}
