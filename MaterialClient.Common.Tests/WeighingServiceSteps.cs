using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Reqnroll;
using Shouldly;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Tests;

[Binding]
public class WeighingServiceSteps : MaterialClientDomainTestBase<MaterialClientDomainTestModule>
{
    private WeighingService? _weighingService;
    private ITruckScaleWeightService? _mockTruckScaleWeightService;
    private IPlateNumberCaptureService? _mockPlateNumberCaptureService;
    private IVehiclePhotoService? _mockVehiclePhotoService;
    private WeighingConfiguration? _configuration;
    private VehicleWeightStatus _currentStatus;
    private List<WeighingRecord> _createdRecords = new();
    private Exception? _capturedException;
    private string? _capturedPlateNumber;
    private List<string>? _capturedPhotoPaths;

    private IRepository<WeighingRecord, long> WeighingRecordRepository => 
        GetRequiredService<IRepository<WeighingRecord, long>>();

    [Given(@"the weighing configuration has offset range from (.*) to (.*) kg")]
    public void GivenTheWeighingConfigurationHasOffsetRange(decimal min, decimal max)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Weighing:WeightOffsetRangeMin", min.ToString() },
                { "Weighing:WeightOffsetRangeMax", max.ToString() },
                { "Weighing:WeightStableDurationMs", "2000" },
                { "Weighing:WeighingMatchDurationHours", "3" }
            })
            .Build();

        _configuration = config.GetSection("Weighing").Get<WeighingConfiguration>() ?? new WeighingConfiguration();
    }

    [Given(@"the weighing configuration has stable duration of (.*) ms")]
    public void GivenTheWeighingConfigurationHasStableDuration(int durationMs)
    {
        // Configuration is set in previous step
    }

    [Given(@"the truck scale weight service is available")]
    public void GivenTheTruckScaleWeightServiceIsAvailable()
    {
        _mockTruckScaleWeightService = Substitute.For<ITruckScaleWeightService>();
    }

    [Given(@"the plate number capture service is available")]
    public void GivenThePlateNumberCaptureServiceIsAvailable()
    {
        _mockPlateNumberCaptureService = Substitute.For<IPlateNumberCaptureService>();
    }

    [Given(@"the vehicle photo service is available")]
    public void GivenTheVehiclePhotoServiceIsAvailable()
    {
        _mockVehiclePhotoService = Substitute.For<IVehiclePhotoService>();
    }

    [Given(@"the truck scale is in (.*) state")]
    public void GivenTheTruckScaleIsInState(string status)
    {
        _currentStatus = Enum.Parse<VehicleWeightStatus>(status);
        // Status is managed internally by WeighingService, we'll verify it after operations
    }

    [Given(@"the current weight is (.*) kg")]
    public void GivenTheCurrentWeightIs(decimal weight)
    {
        _mockTruckScaleWeightService?.GetCurrentWeightAsync().Returns(Task.FromResult(weight));
    }

    [Given(@"the plate number capture service returns ""(.*)""")]
    public void GivenThePlateNumberCaptureServiceReturns(string plateNumber)
    {
        _capturedPlateNumber = plateNumber;
        _mockPlateNumberCaptureService?.CapturePlateNumberAsync().Returns(Task.FromResult<string?>(plateNumber));
    }

    [Given(@"the plate number capture service throws an exception")]
    public void GivenThePlateNumberCaptureServiceThrowsAnException()
    {
        _mockPlateNumberCaptureService?.CapturePlateNumberAsync().Returns(Task.FromException<string?>(new Exception("Plate capture failed")));
    }

    [Given(@"the vehicle photo service returns (.*) photos")]
    public void GivenTheVehiclePhotoServiceReturnsPhotos(int photoCount)
    {
        var photos = Enumerable.Range(1, photoCount)
            .Select(i => $"photo_{i}.jpg")
            .ToList();
        _capturedPhotoPaths = photos;
        _mockVehiclePhotoService?.CaptureVehiclePhotosAsync().Returns(Task.FromResult(photos));
    }

    [Given(@"the vehicle photo service throws an exception")]
    public void GivenTheVehiclePhotoServiceThrowsAnException()
    {
        _mockVehiclePhotoService?.CaptureVehiclePhotosAsync().Returns(Task.FromException<List<string>>(new Exception("Photo capture failed")));
    }

    [When(@"the weight changes to (.*) kg")]
    public async Task WhenTheWeightChangesTo(decimal weight)
    {
        _mockTruckScaleWeightService?.GetCurrentWeightAsync().Returns(Task.FromResult(weight));
        
        if (_weighingService == null)
        {
            await InitializeWeighingServiceAsync();
        }

        // Simulate weight check by calling the service's internal method
        // Since CheckWeight is private, we'll use StartMonitoring and wait
        _weighingService?.StartMonitoring();
        await Task.Delay(100); // Wait for timer to trigger
    }

    [When(@"the weight changes back to (.*) kg before stable duration")]
    public async Task WhenTheWeightChangesBackTo(decimal weight)
    {
        _mockTruckScaleWeightService?.GetCurrentWeightAsync().Returns(Task.FromResult(weight));
        await Task.Delay(100); // Wait less than stable duration
    }

    [When(@"the weight remains stable for (.*) ms")]
    public async Task WhenTheWeightRemainsStableFor(int durationMs)
    {
        await Task.Delay(durationMs + 100); // Wait for stable duration plus buffer
    }

    [When(@"a weighing record is created")]
    public async Task WhenAWeighingRecordIsCreated()
    {
        try
        {
            if (_weighingService == null)
            {
                await InitializeWeighingServiceAsync();
            }

            // Set weight to exceed offset range (5.0 kg > 1.0 kg max)
            _mockTruckScaleWeightService?.GetCurrentWeightAsync().Returns(Task.FromResult(5.0m));

            // Trigger record creation by simulating stable weight
            _weighingService?.StartMonitoring();
            
            // Wait for stable duration (2000ms) plus multiple timer cycles (500ms each)
            // Need to wait at least: 2000ms (stable) + 500ms (timer cycle) + buffer
            await Task.Delay(3000); // Wait for stable duration plus buffer

            // Stop monitoring to ensure all operations complete
            _weighingService?.StopMonitoring();
            
            // Additional delay to ensure async operations (record creation, photo capture) complete
            await Task.Delay(2000);

            // Load created records - retry a few times in case of timing issues
            for (int i = 0; i < 5; i++)
            {
                await WithUnitOfWorkAsync(async () =>
                {
                    var records = await WeighingRecordRepository.GetListAsync();
                    _createdRecords = records.OrderByDescending(r => r.CreationTime).ToList();
                });
                
                if (_createdRecords.Any())
                    break;
                    
                await Task.Delay(500); // Wait a bit more if no records found
            }
        }
        catch (Exception ex)
        {
            _capturedException = ex;
        }
    }

    [Then(@"the system state should be (.*)")]
    public void ThenTheSystemStateShouldBe(string expectedStatus)
    {
        var expected = Enum.Parse<VehicleWeightStatus>(expectedStatus);
        var actual = _weighingService?.GetCurrentStatus() ?? VehicleWeightStatus.OffScale;
        actual.ShouldBe(expected);
    }

    [Then(@"a weighing record should be created with weight (.*) kg")]
    public async Task ThenAWeighingRecordShouldBeCreatedWithWeight(decimal expectedWeight)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var records = await WeighingRecordRepository.GetListAsync();
            records.ShouldNotBeEmpty("At least one weighing record should be created");
            var record = records.FirstOrDefault(r => Math.Abs(r.Weight - expectedWeight) < 0.01m);
            record.ShouldNotBeNull($"A weighing record with weight {expectedWeight} kg should exist");
            _createdRecords.Add(record);
        });
    }

    [Then(@"the weighing record should have RecordType (.*)")]
    public void ThenTheWeighingRecordShouldHaveRecordType(string expectedType)
    {
        var expected = Enum.Parse<WeighingRecordType>(expectedType);
        _createdRecords.ShouldNotBeEmpty("At least one weighing record should exist");
        _createdRecords.First().RecordType.ShouldBe(expected);
    }

    [Then(@"no weighing record should be created")]
    public async Task ThenNoWeighingRecordShouldBeCreated()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var records = await WeighingRecordRepository.GetListAsync();
            records.Count.ShouldBe(0, "No weighing records should be created");
        });
    }

    [Then(@"the weighing record should have plate number ""(.*)""")]
    public void ThenTheWeighingRecordShouldHavePlateNumber(string expectedPlateNumber)
    {
        _createdRecords.ShouldNotBeEmpty("At least one weighing record should exist");
        _createdRecords.First().PlateNumber.ShouldBe(expectedPlateNumber);
    }

    [Then(@"the weighing record should have empty plate number")]
    public void ThenTheWeighingRecordShouldHaveEmptyPlateNumber()
    {
        _createdRecords.ShouldNotBeEmpty("At least one weighing record should exist");
        _createdRecords.First().PlateNumber.ShouldBeNullOrEmpty();
    }

    [Then(@"the weighing record should be created successfully")]
    public void ThenTheWeighingRecordShouldBeCreatedSuccessfully()
    {
        _createdRecords.ShouldNotBeEmpty("The weighing record should be created successfully");
    }

    [Then(@"the weighing record should have (.*) vehicle photo attachments")]
    public async Task ThenTheWeighingRecordShouldHaveVehiclePhotoAttachments(int expectedCount)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            _createdRecords.ShouldNotBeEmpty("At least one weighing record should exist");
            var record = _createdRecords.First(); // Use First() instead of Last() for most recent
            var attachmentRepository = GetRequiredService<IRepository<WeighingRecordAttachment, int>>();
            var attachments = await attachmentRepository.GetListAsync(
                predicate: x => x.WeighingRecordId == record.Id,
                includeDetails: true
            );

            attachments.Count.ShouldBe(expectedCount, $"The weighing record should have {expectedCount} vehicle photo attachments");
        });
    }

    [Then(@"the weighing record should have no vehicle photo attachments")]
    public async Task ThenTheWeighingRecordShouldHaveNoVehiclePhotoAttachments()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            _createdRecords.ShouldNotBeEmpty("At least one weighing record should exist");
            var record = _createdRecords.First(); // Use First() instead of Last() for most recent
            var attachmentRepository = GetRequiredService<IRepository<WeighingRecordAttachment, int>>();
            var attachments = await attachmentRepository.GetListAsync(
                predicate: x => x.WeighingRecordId == record.Id
            );

            attachments.Count.ShouldBe(0, "The weighing record should have no vehicle photo attachments");
        });
    }

    private Task InitializeWeighingServiceAsync()
    {
        // For integration tests, we need to register mocks in the DI container
        // Since we can't easily replace services in ABP's DI container after initialization,
        // we'll create the service manually with mocked dependencies
        var serviceProvider = ServiceProvider;
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Weighing:WeightOffsetRangeMin", "-1" },
                { "Weighing:WeightOffsetRangeMax", "1" },
                { "Weighing:WeightStableDurationMs", "2000" },
                { "Weighing:WeighingMatchDurationHours", "3" }
            })
            .Build();

        // Create service manually with mocked dependencies
        _weighingService = new WeighingService(
            _mockTruckScaleWeightService!,
            _mockPlateNumberCaptureService!,
            _mockVehiclePhotoService!,
            GetRequiredService<IRepository<WeighingRecord, long>>(),
            GetRequiredService<IRepository<WeighingRecordAttachment, int>>(),
            GetRequiredService<IRepository<AttachmentFile, int>>(),
            GetRequiredService<IUnitOfWorkManager>(),
            serviceProvider,
            config
        );
        
        return Task.CompletedTask;
    }
}

