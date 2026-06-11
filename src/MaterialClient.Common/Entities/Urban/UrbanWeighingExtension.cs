using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using MaterialClient.Common.Entities.Enums;
using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities.Urban;

/// <summary>
///     Urban-specific extension entity for weighing records.
///     Associated to <see cref="WeighingRecord" /> by <see cref="WeighingRecordId" /> only (no DB FK / no EF navigation).
/// </summary>
public class UrbanWeighingExtension : Entity<Guid>
{
    /// <summary>
    ///     Parent <see cref="WeighingRecord" /> identifier (logical association, not a database foreign key).
    /// </summary>
    public long WeighingRecordId { get; set; }

    /// <summary>
    ///     Sync status for the Urban upload pipeline.
    ///     Initialized to <see cref="SyncStatus.Pending" /> on creation.
    /// </summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;

    /// <summary>
    ///     Number of retry attempts for background upload.
    ///     Initialized to 0 on creation.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    ///     Timestamp of the last upload failure, or <c>null</c> if no failure has occurred.
    /// </summary>
    public DateTime? LastErrorTime { get; set; }

    /// <summary>
    ///     Indicates whether this weighing record is flagged as anomalous data.
    ///     Set during record creation by <see cref="Services.IUrbanAnomalyDetector" />.
    /// </summary>
    public bool IsAnomaly { get; set; } = false;

    /// <summary>
    ///     Persisted anomaly reason text (e.g. "超上限", "低下限", "车牌为空").
    ///     Set at record creation time and recalculated on approval edits.
    ///     <c>null</c> when the record is not anomalous.
    /// </summary>
    public string? AnomalyReason { get; set; }

    /// <summary>
    ///     JSON array storing the modification history for PlateNumber and TotalWeight edits.
    ///     Each element is an <see cref="EditEntry" /> serialized to JSON.
    ///     <c>null</c> when no edits have been recorded.
    /// </summary>
    public string? EditHistoryJson { get; set; }

    /// <summary>
    ///     Typed access to the modification history.
    ///     Deserializes from / serializes to <see cref="EditHistoryJson" />,
    ///     following the same pattern as <see cref="WeighingRecord.Materials" />.
    /// </summary>
    [NotMapped]
    public List<EditEntry> EditHistory
    {
        get
        {
            if (string.IsNullOrEmpty(EditHistoryJson))
                return new List<EditEntry>();

            try
            {
                return JsonSerializer.Deserialize<List<EditEntry>>(EditHistoryJson)
                       ?? new List<EditEntry>();
            }
            catch
            {
                return new List<EditEntry>();
            }
        }
        set =>
            EditHistoryJson = value == null || value.Count == 0
                ? null
                : JsonSerializer.Serialize(value);
    }
}
