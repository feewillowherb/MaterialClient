using MaterialClient.Common.Entities;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     Product-layer persistence for Recycle waybill extensions.
///     Standard host uses a no-op; Recycle host replaces the implementation.
/// </summary>
public interface IRecycleWaybillExtensionStore : ITransientDependency
{
    Task UpsertPricingAsync(long waybillId, decimal? unitPrice, string? saleContractNo);

    Task UpsertReceivingTimeAsync(long waybillId, DateTime receivingTime);

    Task CopyFromWeighingRecordsAsync(long waybillId, RecycleInfoValues? values);

    Task<RecycleWaybillExtensionSnapshot?> FindByWaybillIdAsync(long waybillId);
}

public record RecycleWaybillExtensionSnapshot(
    decimal? UnitPrice,
    string? SaleContractNo,
    DateTime? ReceivingTime);

public class NullRecycleWaybillExtensionStore : IRecycleWaybillExtensionStore, ITransientDependency
{
    public Task UpsertPricingAsync(long waybillId, decimal? unitPrice, string? saleContractNo) =>
        Task.CompletedTask;

    public Task UpsertReceivingTimeAsync(long waybillId, DateTime receivingTime) => Task.CompletedTask;

    public Task CopyFromWeighingRecordsAsync(long waybillId, RecycleInfoValues? values) => Task.CompletedTask;

    public Task<RecycleWaybillExtensionSnapshot?> FindByWaybillIdAsync(long waybillId) =>
        Task.FromResult<RecycleWaybillExtensionSnapshot?>(null);
}
