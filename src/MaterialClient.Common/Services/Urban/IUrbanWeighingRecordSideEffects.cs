using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.Urban;

/// <summary>
///     Optional Urban-side persistence after kernel weighing-record writes.
///     Standard host uses a no-op; Urban host replaces with Common.Urban implementation.
/// </summary>
public interface IUrbanWeighingRecordSideEffects : ITransientDependency
{
    Task AfterWeighingRecordCreatedAsync(long weighingRecordId);

    Task RecalculateAnomalyAfterLprOrCycleAsync(long weighingRecordId);

    Task AfterWeighingRecordEditedAsync(long weighingRecordId, string plateNumber, decimal totalWeight);
}
