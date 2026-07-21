using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.EntityFrameworkCore;
using MaterialClient.Common.Services;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     DB-backed 单测：RecycleWeighingService.UpdateRecycleModeAsync
///     Waybill → upsert RecycleWaybillExtension；WeighingRecord → ExtraProperties staging。
/// </summary>
public class RecycleWeighingServiceUpsertTests : MaterialClientEntityFrameworkCoreTestBase
{
    private readonly IRecycleWeighingService _recycleWeighingService;
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<RecycleWaybillExtension, Guid> _extensionRepository;

    public RecycleWeighingServiceUpsertTests()
    {
        _recycleWeighingService = GetRequiredService<IRecycleWeighingService>();
        _waybillRepository = GetRequiredService<IRepository<Waybill, long>>();
        _weighingRecordRepository = GetRequiredService<IRepository<WeighingRecord, long>>();
        _extensionRepository = GetRequiredService<IRepository<RecycleWaybillExtension, Guid>>();
    }

    [Fact]
    public async Task UpdateRecycleModeAsync_Inserts_Extension_When_NotExists()
    {
        var waybillId = await CreateWaybillAsync(7001);

        await _recycleWeighingService.UpdateRecycleModeAsync(new UpdateRecycleModeInput(
            waybillId,
            WeighingListItemType.Waybill,
            PlateNumber: "浙A12345",
            ProviderId: null,
            MaterialId: null,
            MaterialUnitId: null,
            DeliveryType: DeliveryType.Sending,
            Remark: null,
            UnitPrice: 120.5m,
            SaleContractNo: "HT-001"));

        var extension = await WithUnitOfWorkAsync(() =>
            _extensionRepository.FirstOrDefaultAsync(e => e.WaybillId == waybillId));

        extension.ShouldNotBeNull();
        extension.WaybillId.ShouldBe(waybillId);
        extension.UnitPrice.ShouldBe(120.5m);
        extension.SaleContractNo.ShouldBe("HT-001");
        extension.ReceivingTime.ShouldBeNull();

        var waybill = await WithUnitOfWorkAsync(() => _waybillRepository.GetAsync(waybillId));
        waybill.IsPendingSync.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateRecycleModeAsync_Updates_Extension_When_Exists_And_Nulls_Fields()
    {
        var waybillId = await CreateWaybillAsync(7002);

        // 预置一条扩展记录（模拟既有数据）
        await WithUnitOfWorkAsync(async () =>
        {
            await _extensionRepository.InsertAsync(new RecycleWaybillExtension(waybillId)
            {
                UnitPrice = 99m,
                SaleContractNo = "OLD",
                ReceivingTime = new DateTime(2026, 1, 1)
            });
        });

        // 传入 null 置空 UnitPrice/SaleContractNo；receivingTime 不变（不传收货时间）
        await _recycleWeighingService.UpdateRecycleModeAsync(new UpdateRecycleModeInput(
            waybillId,
            WeighingListItemType.Waybill,
            PlateNumber: null,
            ProviderId: null,
            MaterialId: null,
            MaterialUnitId: null,
            DeliveryType: null,
            Remark: null,
            UnitPrice: null,
            SaleContractNo: null));

        var extension = await WithUnitOfWorkAsync(() =>
            _extensionRepository.FirstOrDefaultAsync(e => e.WaybillId == waybillId));

        extension.ShouldNotBeNull();
        extension.UnitPrice.ShouldBeNull();
        extension.SaleContractNo.ShouldBeNull();
        // receivingTime 不被 UpdateRecycleModeAsync 修改（仅收货服务写）
        extension.ReceivingTime.ShouldBe(new DateTime(2026, 1, 1));

        // 仅一条扩展记录（upsert 不重复插入）
        var all = await WithUnitOfWorkAsync(() => _extensionRepository.GetListAsync(e => e.WaybillId == waybillId));
        all.Count.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateRecycleModeAsync_WeighingRecord_Writes_ExtraProperties_Without_Extension()
    {
        var recordId = await CreateWeighingRecordAsync(8001);

        await _recycleWeighingService.UpdateRecycleModeAsync(new UpdateRecycleModeInput(
            recordId,
            WeighingListItemType.WeighingRecord,
            PlateNumber: "P8001",
            ProviderId: null,
            MaterialId: null,
            MaterialUnitId: null,
            DeliveryType: DeliveryType.Sending,
            Remark: null,
            UnitPrice: 120.5m,
            SaleContractNo: "HT-WR-001"));

        var record = await WithUnitOfWorkAsync(() => _weighingRecordRepository.GetAsync(recordId));
        record.GetUnitPrice().ShouldBe(120.5m);
        record.GetSaleContractNo().ShouldBe("HT-WR-001");
        record.WeighingMode.ShouldBe(WeighingMode.Recycle);

        var extensions = await WithUnitOfWorkAsync(() => _extensionRepository.GetListAsync());
        extensions.Count.ShouldBe(0);
    }

    [Fact]
    public async Task UpdateRecycleModeAsync_WeighingRecord_Nulls_ExtraProperties()
    {
        var recordId = await CreateWeighingRecordAsync(8002);
        await WithUnitOfWorkAsync(async () =>
        {
            var record = await _weighingRecordRepository.GetAsync(recordId);
            record.SetRecycleInfo(99m, "OLD");
            await _weighingRecordRepository.UpdateAsync(record);
        });

        await _recycleWeighingService.UpdateRecycleModeAsync(new UpdateRecycleModeInput(
            recordId,
            WeighingListItemType.WeighingRecord,
            PlateNumber: null,
            ProviderId: null,
            MaterialId: null,
            MaterialUnitId: null,
            DeliveryType: null,
            Remark: null,
            UnitPrice: null,
            SaleContractNo: null));

        var record = await WithUnitOfWorkAsync(() => _weighingRecordRepository.GetAsync(recordId));
        record.GetUnitPrice().ShouldBeNull();
        record.GetSaleContractNo().ShouldBeNull();
    }

    private async Task<long> CreateWeighingRecordAsync(long id)
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var record = new WeighingRecord(id, 12m)
            {
                WeighingMode = WeighingMode.Recycle,
                PlateNumber = $"P{id}",
                AddDate = DateTime.Now
            };
            await _weighingRecordRepository.InsertAsync(record);
            return id;
        });
    }

    private async Task<long> CreateWaybillAsync(long id)
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var waybill = new Waybill(id, $"fl-{id}")
            {
                WeighingMode = WeighingMode.Recycle,
                OrderGoodsWeight = 10m,
                OrderType = OrderTypeEnum.FirstWeight
            };
            await _waybillRepository.InsertAsync(waybill);
            return id;
        });
    }
}
