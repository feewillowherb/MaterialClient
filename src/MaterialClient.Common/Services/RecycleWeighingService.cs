using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

public interface IRecycleWeighingService
{
    Task UpdateRecycleModeAsync(UpdateRecycleModeInput input);

    /// <summary>
    ///     加载 Recycle 详情页所需字段（含 WeighingRecord ExtraProperties 或 RecycleWaybillExtension）。
    /// </summary>
    Task<RecycleDetailLoadResult?> GetRecycleDetailAsync(long id, WeighingListItemType itemType);
}

[AutoConstructor]
public partial class RecycleWeighingService : DomainService, IRecycleWeighingService, ITransientDependency
{
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IRecycleWaybillExtensionStore _recycleWaybillExtensionStore;
    private readonly ILogger<RecycleWeighingService>? _logger;

    [UnitOfWork]
    public async Task UpdateRecycleModeAsync(UpdateRecycleModeInput input)
    {
        if (input.MaterialId.HasValue && input.MaterialId.Value <= 0)
            throw new BusinessException("MaterialId must be greater than 0 when provided.");

        if (input.MaterialUnitId.HasValue && input.MaterialUnitId.Value <= 0)
            throw new BusinessException("MaterialUnitId must be greater than 0 when provided.");

        if (input.ItemType == WeighingListItemType.WeighingRecord)
        {
            var record = await _weighingRecordRepository.GetAsync(input.Id);

            record.WeighingMode = WeighingMode.Recycle;
            if (input.PlateNumber != null) record.PlateNumber = input.PlateNumber;
            if (input.ProviderId.HasValue) record.ProviderId = input.ProviderId;
            if (input.DeliveryType.HasValue) record.DeliveryType = input.DeliveryType;

            var materials = record.Materials;
            var firstMaterial = materials.FirstOrDefault();
            if (input.MaterialId.HasValue || input.MaterialUnitId.HasValue)
            {
                if (firstMaterial != null)
                {
                    if (input.MaterialId.HasValue) firstMaterial.MaterialId = input.MaterialId;
                    if (input.MaterialUnitId.HasValue) firstMaterial.MaterialUnitId = input.MaterialUnitId;
                    record.Materials = materials;
                }
                else
                {
                    record.AddMaterial(new WeighingRecordMaterial(
                        0,
                        input.MaterialId,
                        input.MaterialUnitId,
                        null));
                }
            }

            // Stage UnitPrice/SaleContractNo on WeighingRecord ExtraProperties before Waybill exists.
            record.SetRecycleInfo(input.UnitPrice, input.SaleContractNo);
            if (input.Remark != null) record.Remark = input.Remark;

            await _weighingRecordRepository.UpdateAsync(record);
            return;
        }

        if (input.ItemType == WeighingListItemType.Waybill)
        {
            var waybill = await _waybillRepository.GetAsync(input.Id);

            waybill.WeighingMode = WeighingMode.Recycle;
            if (input.PlateNumber != null) waybill.PlateNumber = input.PlateNumber;
            if (input.Remark != null) waybill.Remark = input.Remark;
            if (input.ProviderId.HasValue) waybill.ProviderId = input.ProviderId;

            if (input.MaterialId.HasValue) waybill.MaterialId = input.MaterialId;
            if (input.MaterialUnitId.HasValue) waybill.MaterialUnitId = input.MaterialUnitId;
            if (waybill.OrderGoodsWeight.HasValue) waybill.OrderPlanOnPcs = waybill.OrderGoodsWeight;

            // Recycle 扩展字段（UnitPrice/SaleContractNo）upsert 到 RecycleWaybillExtension（每个 Waybill 至多一条）。
            await UpsertRecycleExtensionAsync(
                waybill.Id,
                unitPrice: input.UnitPrice,
                saleContractNo: input.SaleContractNo,
                receivingTime: null);

            waybill.SetPendingSync();
            await _waybillRepository.UpdateAsync(waybill);
            return;
        }

        throw new BusinessException($"Unsupported item type: {input.ItemType}");
    }

    [UnitOfWork]
    public async Task<RecycleDetailLoadResult?> GetRecycleDetailAsync(long id, WeighingListItemType itemType)
    {
        if (itemType == WeighingListItemType.WeighingRecord)
        {
            var record = await _weighingRecordRepository.FindAsync(id);
            if (record == null)
            {
                return null;
            }

            var firstMaterial = record.Materials.FirstOrDefault();
            return new RecycleDetailLoadResult(
                record.ProviderId,
                firstMaterial?.MaterialId,
                firstMaterial?.MaterialUnitId,
                record.GetUnitPrice(),
                record.GetSaleContractNo(),
                record.Remark);
        }

        if (itemType == WeighingListItemType.Waybill)
        {
            var waybill = await _waybillRepository.FindAsync(id);
            if (waybill == null)
            {
                return null;
            }

            var extension = await _recycleWaybillExtensionStore.FindByWaybillIdAsync(waybill.Id);

            return new RecycleDetailLoadResult(
                waybill.ProviderId,
                waybill.MaterialId,
                waybill.MaterialUnitId,
                extension?.UnitPrice,
                extension?.SaleContractNo,
                waybill.Remark);
        }

        throw new BusinessException($"Unsupported item type: {itemType}");
    }

    /// <summary>
    ///     按 <paramref name="waybillId" /> upsert <see cref="RecycleWaybillExtension" />。
    ///     仅更新传入的非 null 字段（receivingTime=null 表示不修改收货时间）。
    ///     存在则更新、否则插入；遵循 UrbanWeighingExtension 约定（无 FK/无导航）。
    /// </summary>
    private async Task UpsertRecycleExtensionAsync(
        long waybillId,
        decimal? unitPrice,
        string? saleContractNo,
        DateTime? receivingTime)
    {
        if (receivingTime.HasValue)
            await _recycleWaybillExtensionStore.UpsertReceivingTimeAsync(waybillId, receivingTime.Value);

        await _recycleWaybillExtensionStore.UpsertPricingAsync(waybillId, unitPrice, saleContractNo);
    }
}

/// <summary>
///     Recycle 详情页加载结果（WeighingRecord ExtraProperties 或 RecycleWaybillExtension）。
/// </summary>
public record RecycleDetailLoadResult(
    int? ProviderId,
    int? MaterialId,
    int? MaterialUnitId,
    decimal? UnitPrice,
    string? SaleContractNo,
    string? Remark
);

public record UpdateRecycleModeInput(
    long Id,
    WeighingListItemType ItemType,
    string? PlateNumber,
    int? ProviderId,
    int? MaterialId,
    int? MaterialUnitId,
    DeliveryType? DeliveryType,
    string? Remark,
    decimal? UnitPrice = null,
    string? SaleContractNo = null
);
