using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.Urban;

/// <summary>
///     Optional Urban-side persistence after kernel weighing-record writes.
///     Standard host uses a no-op; Urban host replaces with Common.Urban implementation.
/// </summary>
public interface IUrbanWeighingRecordSideEffects : ITransientDependency
{
    Task AfterWeighingRecordCreatedAsync(long weighingRecordId);

    /// <summary>
    ///     Mid-cycle anomaly refresh (e.g. after LPR). Does not promote WeighingInProgress to Pending.
    ///     If already Synced/Failed and anomaly state changes, may return SyncStatus to Pending.
    /// </summary>
    Task RecalculateAnomalyAfterLprOrCycleAsync(long weighingRecordId);

    /// <summary>
    ///     Cycle complete (off-scale): formal anomaly evaluation and promote WeighingInProgress to Pending.
    /// </summary>
    Task FinalizeWeighingCycleAsync(long weighingRecordId);

    /// <summary>
    ///     Ensure extension is upload-eligible: evaluate anomaly and promote WeighingInProgress to Pending.
    /// </summary>
    Task EnsureReadyForUploadAsync(long weighingRecordId);

    Task AfterWeighingRecordEditedAsync(long weighingRecordId, string plateNumber, decimal totalWeight);
}
