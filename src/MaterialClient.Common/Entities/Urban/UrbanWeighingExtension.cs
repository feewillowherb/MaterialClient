using MaterialClient.Common.Entities.Enums;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities.Urban;

/// <summary>
///     Urban-specific extension entity for weighing records.
///     Associated to <see cref="WeighingRecord" /> by <see cref="WeighingRecordId" /> only (no DB FK / no EF navigation).
/// </summary>
public class UrbanWeighingExtension : Entity<Guid>, IHasExtraProperties
{
    private ExtraPropertyDictionary _extraProperties = new();

    /// <summary>
    ///     额外属性字典
    /// </summary>
    public ExtraPropertyDictionary ExtraProperties
    {
        get => _extraProperties;
        set => _extraProperties = value;
    }

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
    ///     Persisted anomaly reason enum.
    ///     Set at record creation time and recalculated on approval edits.
    ///     <c>null</c> when the record is not anomalous.
    /// </summary>
    public AnomalyReason? AnomalyReason { get; set; }
}
