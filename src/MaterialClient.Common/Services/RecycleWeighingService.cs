using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

public interface IRecycleWeighingService
{
    Task UpdateRecycleModeAsync(UpdateRecycleModeInput input);
}

[AutoConstructor]
public partial class RecycleWeighingService : DomainService, IRecycleWeighingService, ITransientDependency
{
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<Waybill, long> _waybillRepository;
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

            waybill.SetPendingSync();
            await _waybillRepository.UpdateAsync(waybill);
            return;
        }

        throw new BusinessException($"Unsupported item type: {input.ItemType}");
    }
}

public record UpdateRecycleModeInput(
    long Id,
    WeighingListItemType ItemType,
    string? PlateNumber,
    int? ProviderId,
    int? MaterialId,
    int? MaterialUnitId,
    DeliveryType? DeliveryType,
    string? Remark
);
