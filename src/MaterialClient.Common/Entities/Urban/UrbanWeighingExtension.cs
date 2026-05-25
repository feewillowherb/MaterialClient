using MaterialClient.Common.Entities.Enums;
using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities.Urban;

/// <summary>
///     Urban-specific extension entity for weighing records.
///     Maintains a 1:0..1 relationship with <see cref="WeighingRecord" />,
///     storing Urban-variant-specific fields such as sync status, retry count, and error tracking.
/// </summary>
public class UrbanWeighingExtension : Entity<Guid>
{
    /// <summary>
    ///     Foreign key referencing the parent <see cref="WeighingRecord" />.
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
    ///     Navigation property back to the parent <see cref="WeighingRecord" />.
    /// </summary>
    public WeighingRecord WeighingRecord { get; set; } = null!;
}
