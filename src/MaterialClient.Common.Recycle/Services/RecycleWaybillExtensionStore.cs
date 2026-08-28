using MaterialClient.Common.Entities;
using MaterialClient.Common.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Recycle.Services;

[Dependency(ReplaceServices = true)]
public class RecycleWaybillExtensionStore : IRecycleWaybillExtensionStore, ITransientDependency
{
    private readonly IRepository<RecycleWaybillExtension, Guid> _repository;

    public RecycleWaybillExtensionStore(IRepository<RecycleWaybillExtension, Guid> repository)
    {
        _repository = repository;
    }

    [UnitOfWork]
    public virtual async Task UpsertPricingAsync(long waybillId, decimal? unitPrice, string? saleContractNo)
    {
        var existing = await _repository.FirstOrDefaultAsync(e => e.WaybillId == waybillId);
        var sale = string.IsNullOrWhiteSpace(saleContractNo) ? null : saleContractNo;
        if (existing == null)
        {
            await _repository.InsertAsync(new RecycleWaybillExtension(waybillId)
            {
                UnitPrice = unitPrice,
                SaleContractNo = sale
            }, true);
            return;
        }

        existing.UnitPrice = unitPrice;
        existing.SaleContractNo = sale;
        await _repository.UpdateAsync(existing, true);
    }

    [UnitOfWork]
    public virtual async Task UpsertReceivingTimeAsync(long waybillId, DateTime receivingTime)
    {
        var existing = await _repository.FirstOrDefaultAsync(e => e.WaybillId == waybillId);
        if (existing == null)
        {
            await _repository.InsertAsync(new RecycleWaybillExtension(waybillId)
            {
                ReceivingTime = receivingTime
            }, true);
            return;
        }

        existing.ReceivingTime = receivingTime;
        await _repository.UpdateAsync(existing, true);
    }

    [UnitOfWork]
    public virtual async Task CopyFromWeighingRecordsAsync(long waybillId, RecycleInfoValues? values)
    {
        if (values == null || !values.HasAnyValue)
            return;

        await UpsertPricingAsync(waybillId, values.UnitPrice, values.SaleContractNo);
    }

    [UnitOfWork]
    public virtual async Task<RecycleWaybillExtensionSnapshot?> FindByWaybillIdAsync(long waybillId)
    {
        var existing = await _repository.FirstOrDefaultAsync(e => e.WaybillId == waybillId);
        if (existing == null)
            return null;

        return new RecycleWaybillExtensionSnapshot(
            existing.UnitPrice,
            existing.SaleContractNo,
            existing.ReceivingTime);
    }
}
