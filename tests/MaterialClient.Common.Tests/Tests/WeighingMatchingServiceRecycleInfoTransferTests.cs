using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.EntityFrameworkCore;
using MaterialClient.Common.Services;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     DB-backed：匹配建单时将 WeighingRecord ExtraProperties 中的 RecycleInfo
///     拷贝到 RecycleWaybillExtension（对照 SolidWaste transfer）。
/// </summary>
public class WeighingMatchingServiceRecycleInfoTransferTests : MaterialClientEntityFrameworkCoreTestBase
{
    private readonly IWeighingMatchingService _matchingService;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<RecycleWaybillExtension, Guid> _extensionRepository;

    public WeighingMatchingServiceRecycleInfoTransferTests()
    {
        _matchingService = GetRequiredService<IWeighingMatchingService>();
        _weighingRecordRepository = GetRequiredService<IRepository<WeighingRecord, long>>();
        _extensionRepository = GetRequiredService<IRepository<RecycleWaybillExtension, Guid>>();
    }

    [Fact]
    public async Task ManualMatch_Copies_Join_RecycleInfo_To_Extension()
    {
        var (joinId, outId) = await CreateMatchableRecyclePairAsync(
            joinId: 9201,
            outId: 9202,
            joinUnitPrice: 120m,
            joinContractNo: "HT-JOIN",
            outUnitPrice: null,
            outContractNo: null);

        var join = await WithUnitOfWorkAsync(() => _weighingRecordRepository.GetAsync(joinId));
        var outRecord = await WithUnitOfWorkAsync(() => _weighingRecordRepository.GetAsync(outId));

        var waybill = await _matchingService.ManualMatchAsync(join, outRecord, DeliveryType.Receiving);

        var extension = await WithUnitOfWorkAsync(() =>
            _extensionRepository.FirstOrDefaultAsync(e => e.WaybillId == waybill.Id));

        extension.ShouldNotBeNull();
        extension!.UnitPrice.ShouldBe(120m);
        extension.SaleContractNo.ShouldBe("HT-JOIN");
        extension.ReceivingTime.ShouldBeNull();
    }

    [Fact]
    public async Task ManualMatch_Fallback_Out_RecycleInfo_When_Join_Missing()
    {
        var (joinId, outId) = await CreateMatchableRecyclePairAsync(
            joinId: 9211,
            outId: 9212,
            joinUnitPrice: null,
            joinContractNo: null,
            outUnitPrice: 80m,
            outContractNo: "HT-OUT");

        var join = await WithUnitOfWorkAsync(() => _weighingRecordRepository.GetAsync(joinId));
        var outRecord = await WithUnitOfWorkAsync(() => _weighingRecordRepository.GetAsync(outId));

        var waybill = await _matchingService.ManualMatchAsync(join, outRecord, DeliveryType.Receiving);

        var extension = await WithUnitOfWorkAsync(() =>
            _extensionRepository.FirstOrDefaultAsync(e => e.WaybillId == waybill.Id));

        extension.ShouldNotBeNull();
        extension!.UnitPrice.ShouldBe(80m);
        extension.SaleContractNo.ShouldBe("HT-OUT");
    }

    [Fact]
    public async Task ManualMatch_BothEmpty_Does_Not_Insert_Extension()
    {
        var (joinId, outId) = await CreateMatchableRecyclePairAsync(
            joinId: 9221,
            outId: 9222,
            joinUnitPrice: null,
            joinContractNo: null,
            outUnitPrice: null,
            outContractNo: null);

        var join = await WithUnitOfWorkAsync(() => _weighingRecordRepository.GetAsync(joinId));
        var outRecord = await WithUnitOfWorkAsync(() => _weighingRecordRepository.GetAsync(outId));

        var waybill = await _matchingService.ManualMatchAsync(join, outRecord, DeliveryType.Receiving);

        var extension = await WithUnitOfWorkAsync(() =>
            _extensionRepository.FirstOrDefaultAsync(e => e.WaybillId == waybill.Id));

        extension.ShouldBeNull();
    }

    [Fact]
    public async Task ManualMatch_NonRecycle_Does_Not_Insert_Extension()
    {
        var now = DateTime.Now;
        var joinId = await InsertRecordAsync(9231, 20m, now.AddMinutes(-10), WeighingMode.Standard, null, null);
        var outId = await InsertRecordAsync(9232, 10m, now, WeighingMode.Standard, 50m, "SHOULD-NOT-COPY");

        var join = await WithUnitOfWorkAsync(() => _weighingRecordRepository.GetAsync(joinId));
        var outRecord = await WithUnitOfWorkAsync(() => _weighingRecordRepository.GetAsync(outId));

        var waybill = await _matchingService.ManualMatchAsync(join, outRecord, DeliveryType.Receiving);

        waybill.WeighingMode.ShouldNotBe(WeighingMode.Recycle);

        var extension = await WithUnitOfWorkAsync(() =>
            _extensionRepository.FirstOrDefaultAsync(e => e.WaybillId == waybill.Id));

        extension.ShouldBeNull();
    }

    /// <summary>
    ///     Receiving: earlier heavier = join, later lighter = out; weight diff &gt; ManualMatchMinWeightDiff.
    /// </summary>
    private async Task<(long JoinId, long OutId)> CreateMatchableRecyclePairAsync(
        long joinId,
        long outId,
        decimal? joinUnitPrice,
        string? joinContractNo,
        decimal? outUnitPrice,
        string? outContractNo)
    {
        var now = DateTime.Now;
        await InsertRecordAsync(
            joinId,
            20m,
            now.AddMinutes(-10),
            WeighingMode.Recycle,
            joinUnitPrice,
            joinContractNo);
        await InsertRecordAsync(
            outId,
            10m,
            now,
            WeighingMode.Recycle,
            outUnitPrice,
            outContractNo);
        return (joinId, outId);
    }

    private async Task<long> InsertRecordAsync(
        long id,
        decimal weight,
        DateTime addDate,
        WeighingMode mode,
        decimal? unitPrice,
        string? saleContractNo)
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var record = new WeighingRecord(id, weight)
            {
                WeighingMode = mode,
                PlateNumber = $"P{id}",
                AddDate = addDate,
                DeliveryType = DeliveryType.Receiving
            };
            if (unitPrice.HasValue || !string.IsNullOrWhiteSpace(saleContractNo))
            {
                record.SetRecycleInfo(unitPrice, saleContractNo);
            }

            await _weighingRecordRepository.InsertAsync(record);
            return id;
        });
    }
}
