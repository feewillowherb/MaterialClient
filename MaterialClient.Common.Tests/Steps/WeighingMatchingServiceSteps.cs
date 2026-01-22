using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Reqnroll;
using Shouldly;
using MaterialClient.Common;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Tests;

[Binding]
public class WeighingMatchingServiceSteps : MaterialClientDomainTestBase<MaterialClientDomainTestModule>
{
    private List<WeighingRecord> _testRecords = new();
    private List<Waybill> _createdWaybills = new();
    private DeliveryType _deliveryType = DeliveryType.Receiving;
    private int _waybillsCreatedCount;

    private TestManager M => GetRequiredService<TestManager>();

    [Given(@"the weighing configuration has match duration of (.*) hours")]
    public void GivenTheWeighingConfigurationHasMatchDuration(int hours)
    {
        // Configuration is handled by the test module
    }

    [Given(@"the weighing record repository is available")]
    public void GivenTheWeighingRecordRepositoryIsAvailable()
    {
        // Repository is available through DI
    }

    [Given(@"the waybill repository is available")]
    public void GivenTheWaybillRepositoryIsAvailable()
    {
        // Repository is available through DI
    }

    [Given(@"Weighing records as below")]
    public async Task GivenWeighingRecordsAsBelow(Table table)
    {
        var infos = table.CreateSet<WeighingRecordTestDto>().ToList();

        await WithUnitOfWorkAsync(async () =>
        {
            _testRecords.Clear();
            foreach (var info in infos)
            {
                var record = new WeighingRecord(info.Weight)
                {
                    PlateNumber = info.PlateNumber,
                    ProviderId = info.ProviderId
                };

                // Set AddDate before inserting
                var creationTimeValue = DateTime.Parse(info.CreatedAt);
                record.AddDate = creationTimeValue;
                
                await M.WeighingRecordRepository.InsertAsync(record);

                _testRecords.Add(record);
            }
        });
    }


    [Given(@"the delivery type is (.*)")]
    public void GivenTheDeliveryTypeIs(string deliveryType)
    {
        _deliveryType = Enum.Parse<DeliveryType>(deliveryType);
    }

    [When(@"matching is performed")]
    public async Task WhenMatchingIsPerformed()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            // Try to match each test record
            foreach (var record in _testRecords)
            {
                await M.MatchingService.AutoMatchAsync(record.Id);
            }

            // Load created waybills
            var waybills = await M.WaybillRepository.GetListAsync();
            _createdWaybills = waybills.ToList();
            _waybillsCreatedCount = _createdWaybills.Count;

            // Reload test records to get updated MatchedType
            var allRecords = await M.WeighingRecordRepository.GetListAsync();
            _testRecords = allRecords.Where(r => _testRecords.Any(tr => tr.Id == r.Id)).ToList();
        });
    }

    [Then(@"Waybills as below")]
    public async Task ThenWaybillsAsBelow(Table table)
    {
        var infos = table.CreateSet<WaybillVerifyTestDto>().ToList();

        var waybills = await M.WaybillRepository.GetListAsync();
        waybills.Count.ShouldBe(infos.Count, $"Expected {infos.Count} waybill(s), but found {waybills.Count}");

        foreach (var info in infos)
        {
            var waybill = waybills.FirstOrDefault(w => w.PlateNumber == info.PlateNumber);
            waybill.ShouldNotBeNull($"Waybill with plate number {info.PlateNumber} should exist");

            if (info.OrderTruckWeight.HasValue)
            {
                waybill.OrderTruckWeight.ShouldNotBeNull("OrderTruckWeight should not be null");
                waybill.OrderTruckWeight!.Value.ShouldBe(info.OrderTruckWeight.Value, 0.01m);
            }

            if (info.OrderTotalWeight.HasValue)
            {
                waybill.OrderTotalWeight.ShouldNotBeNull("OrderTotalWeight should not be null");
                waybill.OrderTotalWeight!.Value.ShouldBe(info.OrderTotalWeight.Value, 0.01m);
            }

            if (info.OrderGoodsWeight.HasValue)
            {
                waybill.OrderGoodsWeight.ShouldNotBeNull("OrderGoodsWeight should not be null");
                waybill.OrderGoodsWeight!.Value.ShouldBe(info.OrderGoodsWeight.Value, 0.01m);
            }

            if (!string.IsNullOrEmpty(info.JoinTime))
            {
                var expectedJoinTime = DateTime.Parse(info.JoinTime);
                waybill.JoinTime.ShouldNotBeNull("JoinTime should not be null");
                waybill.JoinTime!.Value.ShouldBe(expectedJoinTime, TimeSpan.FromSeconds(1));
            }

            if (!string.IsNullOrEmpty(info.OutTime))
            {
                var expectedOutTime = DateTime.Parse(info.OutTime);
                waybill.OutTime.ShouldNotBeNull("OutTime should not be null");
                waybill.OutTime!.Value.ShouldBe(expectedOutTime, TimeSpan.FromSeconds(1));
            }

            if (info.ProviderId.HasValue)
            {
                waybill.ProviderId.ShouldBe(info.ProviderId.Value);
            }

            // Verify record matched types if specified
            if (!string.IsNullOrEmpty(info.Record1MatchedType) || !string.IsNullOrEmpty(info.Record2MatchedType))
            {
                var records = await M.WeighingRecordRepository.GetListAsync(r => r.WaybillId == waybill.Id);
                // Order by Id as records are created sequentially
                var recordList = records.OrderBy(r => r.Id).ToList();

                if (!string.IsNullOrEmpty(info.Record1MatchedType) && recordList.Count > 0)
                {
                    var expectedType = Enum.Parse<WeighingRecordMatchType>(info.Record1MatchedType);
                    recordList[0].MatchedType.ShouldBe(expectedType, $"Record 1 should have MatchedType {info.Record1MatchedType}");
                }

                if (!string.IsNullOrEmpty(info.Record2MatchedType) && recordList.Count > 1)
                {
                    var expectedType = Enum.Parse<WeighingRecordMatchType>(info.Record2MatchedType);
                    recordList[1].MatchedType.ShouldBe(expectedType, $"Record 2 should have MatchedType {info.Record2MatchedType}");
                }
            }
        }
    }

    [Then(@"(\d+) waybill(?:s)? should be created")]
    public void ThenWaybillShouldBeCreated(int expectedCount)
    {
        _waybillsCreatedCount.ShouldBe(expectedCount, $"{expectedCount} waybill(s) should be created");
        _createdWaybills.Count.ShouldBe(expectedCount, $"{expectedCount} waybill(s) should exist in repository");
    }

    [Then(@"Weighing records as below")]
    public async Task ThenWeighingRecordsAsBelow(Table table)
    {
        var infos = table.CreateSet<WeighingRecordVerifyTestDto>().ToList();

        var records = await M.WeighingRecordRepository.GetListAsync();
        records.Count.ShouldBeGreaterThanOrEqualTo(infos.Count, $"Expected at least {infos.Count} record(s), but found {records.Count}");

        foreach (var info in infos)
        {
            var record = records.FirstOrDefault(r => r.PlateNumber == info.PlateNumber && 
                                                     Math.Abs(r.TotalWeight - info.Weight) < 0.01m);
            record.ShouldNotBeNull($"Weighing record with plate number {info.PlateNumber} and weight {info.Weight} should exist");

            if (!string.IsNullOrEmpty(info.MatchedType))
            {
                var expectedType = Enum.Parse<WeighingRecordMatchType>(info.MatchedType);
                record.MatchedType.ShouldBe(expectedType, $"Record with plate {info.PlateNumber} should have MatchedType {info.MatchedType}");
            }
            else
            {
                record.MatchedType.ShouldBeNull($"Record with plate {info.PlateNumber} should have MatchedType null (Unmatch)");
            }
        }
    }

}

file record WeighingRecordTestDto(string PlateNumber, decimal Weight, string CreatedAt, int? ProviderId = null);

file record WeighingRecordVerifyTestDto(string PlateNumber, decimal Weight, string MatchedType);

file record WaybillVerifyTestDto(
    string PlateNumber,
    decimal? OrderTruckWeight,
    decimal? OrderTotalWeight,
    decimal? OrderGoodsWeight,
    string? JoinTime,
    string? OutTime,
    int? ProviderId,
    string? Record1MatchedType,
    string? Record2MatchedType
);

[AutoConstructor]
internal sealed partial class TestManager
{
    [field: AutoConstructorInject] public IRepository<WeighingRecord, long> WeighingRecordRepository { get; }
    [field: AutoConstructorInject] public IRepository<Waybill, long> WaybillRepository { get; }
    [field: AutoConstructorInject] public MaterialClient.Common.Services.WeighingMatchingService MatchingService { get; }
}
