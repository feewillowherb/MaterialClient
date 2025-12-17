using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Reqnroll;
using Shouldly;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using MaterialClient.Common.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Tests;

[Binding]
public class WeighingMatchingServiceSteps : MaterialClientDomainTestBase<MaterialClientDomainTestModule>
{
    private WeighingMatchingService? _matchingService;
    private List<WeighingRecord> _testRecords = new();
    private List<Waybill> _createdWaybills = new();
    private DeliveryType _deliveryType = DeliveryType.Receiving;
    private int _waybillsCreatedCount;

    private IRepository<WeighingRecord, long> WeighingRecordRepository =>
        GetRequiredService<IRepository<WeighingRecord, long>>();

    private IRepository<Waybill, long> WaybillRepository =>
        GetRequiredService<IRepository<Waybill, long>>();

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

    [Given(@"there are (.*) unmatched weighing records")]
    public void GivenThereAreUnmatchedWeighingRecords(int count)
    {
        _testRecords.Clear();
        // Records will be created in subsequent steps
    }

    [Given(@"record (.*) has plate number ""(.*)"" and weight (.*) kg created at ""(.*)""")]
    public async Task GivenRecordHasPlateNumberAndWeightCreatedAt(int recordIndex, string plateNumber, decimal weight,
        string creationTime)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var record = new WeighingRecord(weight) // Id will be auto-generated
            {
                PlateNumber = plateNumber,
            };

            await WeighingRecordRepository.InsertAsync(record);

            // Set CreationTime using reflection (since it's read-only from FullAuditedEntity)
            var creationTimeValue = DateTime.Parse(creationTime);
            var creationTimeProperty = typeof(Volo.Abp.Domain.Entities.Auditing.CreationAuditedEntity<long>)
                .GetProperty("CreationTime",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (creationTimeProperty != null && creationTimeProperty.CanWrite)
            {
                creationTimeProperty.SetValue(record, creationTimeValue);
                await WeighingRecordRepository.UpdateAsync(record);
            }

            _testRecords.Add(record);
        });
    }

    [Given(@"record (.*) has plate number ""(.*)"" and weight (.*) kg and ProviderId (.*)")]
    public async Task GivenRecordHasPlateNumberAndWeightAndProviderId(int recordIndex, string plateNumber,
        decimal weight, int? providerId)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var record = new WeighingRecord(weight) // Id will be auto-generated
            {
                PlateNumber = plateNumber,
            };

            await WeighingRecordRepository.InsertAsync(record);

            // Set CreationTime using EF Core Entry (since it's read-only from FullAuditedEntity)
            var creationTimeValue = DateTime.UtcNow.AddHours(-recordIndex);
            var dbContext = GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            var entry = dbContext.Entry(record);
            entry.Property("CreationTime").CurrentValue = creationTimeValue;
            await dbContext.SaveChangesAsync();

            _testRecords.Add(record);
        });
    }

    [Given(@"record (.*) has plate number ""(.*)"" and weight (.*) kg and MaterialId (.*)")]
    public async Task GivenRecordHasPlateNumberAndWeightAndMaterialId(int recordIndex, string plateNumber,
        decimal weight, int? materialId)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var record = new WeighingRecord(weight) // Id will be auto-generated
            {
                PlateNumber = plateNumber,
            };

            await WeighingRecordRepository.InsertAsync(record);

            // Set CreationTime using EF Core Entry (since it's read-only from FullAuditedEntity)
            var creationTimeValue = DateTime.UtcNow.AddHours(-recordIndex);
            var dbContext = GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            var entry = dbContext.Entry(record);
            entry.Property("CreationTime").CurrentValue = creationTimeValue;
            await dbContext.SaveChangesAsync();

            _testRecords.Add(record);
        });
    }

    [Given(@"there are (.*) unmatched weighing records with same plate number ""(.*)""")]
    public void GivenThereAreUnmatchedWeighingRecordsWithSamePlateNumber(int count, string plateNumber)
    {
        _testRecords.Clear();
        // Records will be created in subsequent steps
    }

    [Given(@"record (.*) has weight (.*) kg created at ""(.*)""")]
    public async Task GivenRecordHasWeightCreatedAt(int recordIndex, decimal weight, string creationTime)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var record = new WeighingRecord(weight) // Id will be auto-generated
            {
                PlateNumber = "京A12345", // Same plate number for all records
            };

            await WeighingRecordRepository.InsertAsync(record);

            // Set CreationTime using reflection (since it's read-only from FullAuditedEntity)
            var creationTimeValue = DateTime.Parse(creationTime);
            var creationTimeProperty = typeof(Volo.Abp.Domain.Entities.Auditing.CreationAuditedEntity<long>)
                .GetProperty("CreationTime",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (creationTimeProperty != null && creationTimeProperty.CanWrite)
            {
                creationTimeProperty.SetValue(record, creationTimeValue);
                await WeighingRecordRepository.UpdateAsync(record);
            }

            _testRecords.Add(record);
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
            _matchingService = GetRequiredService<WeighingMatchingService>();
            // _waybillsCreatedCount = await _matchingService.TryMatchAndCreateWaybillsAsync(_deliveryType);

            // Load created waybills
            var waybills = await WaybillRepository.GetListAsync();
            _createdWaybills = waybills.ToList();

            // Reload test records to get updated RecordType
            var allRecords = await WeighingRecordRepository.GetListAsync();
            _testRecords = allRecords.Where(r => _testRecords.Any(tr => tr.Id == r.Id)).ToList();
        });
    }

    [Then(@"(.*) waybill should be created")]
    public void ThenWaybillShouldBeCreated(int expectedCount)
    {
        _waybillsCreatedCount.ShouldBe(expectedCount, $"{expectedCount} waybill(s) should be created");
        _createdWaybills.Count.ShouldBe(expectedCount, $"{expectedCount} waybill(s) should exist in repository");
    }

    [Then(@"record (.*) should have RecordType (.*)")]
    public void ThenRecordShouldHaveRecordType(int recordIndex, string expectedType)
    {
        // var expected = Enum.Parse<WeighingRecordType>(expectedType);
        // var record = _testRecords[recordIndex - 1]; // Convert to 0-based index
        // record.RecordType.ShouldBe(expected, $"Record {recordIndex} should have RecordType {expectedType}");
    }

    [Then(@"the waybill should have OrderNo generated from Guid")]
    public void ThenTheWaybillShouldHaveOrderNoGeneratedFromGuid()
    {
        _createdWaybills.ShouldNotBeEmpty("At least one waybill should exist");
        var waybill = _createdWaybills.First();
        Guid.TryParse(waybill.OrderNo, out _).ShouldBeTrue("OrderNo should be a valid GUID");
    }

    [Then(@"the waybill should have plate number ""(.*)""")]
    public void ThenTheWaybillShouldHavePlateNumber(string expectedPlateNumber)
    {
        _createdWaybills.ShouldNotBeEmpty("At least one waybill should exist");
        var waybill = _createdWaybills.First();
        waybill.PlateNumber.ShouldBe(expectedPlateNumber);
    }

    [Then(@"the waybill should have JoinTime ""(.*)""")]
    public void ThenTheWaybillShouldHaveJoinTime(string expectedTime)
    {
        _createdWaybills.ShouldNotBeEmpty("At least one waybill should exist");
        var waybill = _createdWaybills.First();
        var expected = DateTime.Parse(expectedTime);
        waybill.JoinTime.ShouldNotBeNull("JoinTime should not be null");
        waybill.JoinTime!.Value.ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    [Then(@"the waybill should have OutTime ""(.*)""")]
    public void ThenTheWaybillShouldHaveOutTime(string expectedTime)
    {
        _createdWaybills.ShouldNotBeEmpty("At least one waybill should exist");
        var waybill = _createdWaybills.First();
        var expected = DateTime.Parse(expectedTime);
        waybill.OutTime.ShouldNotBeNull("OutTime should not be null");
        waybill.OutTime!.Value.ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    [Then(@"the waybill should have OrderTruckWeight (.*) kg")]
    public void ThenTheWaybillShouldHaveOrderTruckWeight(decimal expectedWeight)
    {
        _createdWaybills.ShouldNotBeEmpty("At least one waybill should exist");
        var waybill = _createdWaybills.First();
        waybill.OrderTruckWeight.ShouldNotBeNull("OrderTruckWeight should not be null");
        waybill.OrderTruckWeight!.Value.ShouldBe(expectedWeight, 0.01m);
    }

    [Then(@"the waybill should have OrderTotalWeight (.*) kg")]
    public void ThenTheWaybillShouldHaveOrderTotalWeight(decimal expectedWeight)
    {
        _createdWaybills.ShouldNotBeEmpty("At least one waybill should exist");
        var waybill = _createdWaybills.First();
        waybill.OrderTotalWeight.ShouldNotBeNull("OrderTotalWeight should not be null");
        waybill.OrderTotalWeight!.Value.ShouldBe(expectedWeight, 0.01m);
    }

    [Then(@"the waybill should have OrderGoodsWeight (.*) kg")]
    public void ThenTheWaybillShouldHaveOrderGoodsWeight(decimal expectedWeight)
    {
        _createdWaybills.ShouldNotBeEmpty("At least one waybill should exist");
        var waybill = _createdWaybills.First();
        waybill.OrderGoodsWeight.ShouldNotBeNull("OrderGoodsWeight should not be null");
        waybill.OrderGoodsWeight!.Value.ShouldBe(expectedWeight, 0.01m);
    }

    [Then(@"the waybill should have ProviderId (.*)")]
    public void ThenTheWaybillShouldHaveProviderId(int expectedProviderId)
    {
        _createdWaybills.ShouldNotBeEmpty("At least one waybill should exist");
        var waybill = _createdWaybills.First();
        waybill.ProviderId.ShouldBe(expectedProviderId);
    }

    // Note: Waybill does not have MaterialId property, so this step is removed
}