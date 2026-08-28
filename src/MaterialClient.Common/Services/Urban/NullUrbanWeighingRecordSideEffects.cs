using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.Urban;

public class NullUrbanWeighingRecordSideEffects : IUrbanWeighingRecordSideEffects, ITransientDependency
{
    public Task AfterWeighingRecordCreatedAsync(long weighingRecordId) => Task.CompletedTask;

    public Task RecalculateAnomalyAfterLprOrCycleAsync(long weighingRecordId) => Task.CompletedTask;

    public Task AfterWeighingRecordEditedAsync(long weighingRecordId, string plateNumber, decimal totalWeight) =>
        Task.CompletedTask;
}
