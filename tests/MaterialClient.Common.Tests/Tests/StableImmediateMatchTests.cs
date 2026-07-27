using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.AttendedWeighing;
using MaterialClient.Common.Services.Urban;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
/// Tests for EnableMatchOnStable: early TryMatch on stable and OffScale fallback/skip rules.
/// </summary>
public class StableImmediateMatchTests
{
    private readonly TestLocalEventBus _eventBus = new();
    private readonly List<TryMatchEvent> _published = [];

    public StableImmediateMatchTests()
    {
        _eventBus.Subscribe<TryMatchEvent>(e =>
        {
            _published.Add(e);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TryPublishMatchOnStable_Should_Publish_WhenEnabledAndPlatePresent()
    {
        var (service, stateManager, _) = CreateSut(enableMatchOnStable: true, plate: "京A12345");
        stateManager.SetLastCreatedWeighingRecordId(42);

        await service.TryPublishMatchOnStableAsync(stateManager);

        _published.Count.ShouldBe(1);
        _published[0].WeighingRecordId.ShouldBe(42);
    }

    [Fact]
    public async Task TryPublishMatchOnStable_Should_NotPublish_WhenDisabled()
    {
        var (service, stateManager, _) = CreateSut(enableMatchOnStable: false, plate: "京A12345");
        stateManager.SetLastCreatedWeighingRecordId(42);

        await service.TryPublishMatchOnStableAsync(stateManager);

        _published.ShouldBeEmpty();
    }

    [Fact]
    public async Task TryPublishMatchOnStable_Should_NotPublish_WhenPlateEmpty()
    {
        var (service, stateManager, _) = CreateSut(enableMatchOnStable: true, plate: null);
        stateManager.SetLastCreatedWeighingRecordId(42);

        await service.TryPublishMatchOnStableAsync(stateManager);

        _published.ShouldBeEmpty();
    }

    [Fact]
    public async Task TryPublishMatchOnStable_Should_NotPublish_WhenSkipWaybillMatching()
    {
        var (service, stateManager, _) = CreateSut(
            enableMatchOnStable: true,
            plate: "京A12345",
            skipWaybillMatching: true);
        stateManager.SetLastCreatedWeighingRecordId(42);

        await service.TryPublishMatchOnStableAsync(stateManager);

        _published.ShouldBeEmpty();
    }

    [Fact]
    public async Task TryReWrite_Should_SkipTryMatch_WhenAlreadyMatchedAndMatchOnStableEnabled()
    {
        var record = new WeighingRecord(10, 12.5m) { PlateNumber = "京A12345" };
        record.MatchAsJoin(99, 100);

        var (service, stateManager, plateService) = CreateSut(
            enableMatchOnStable: true,
            plate: "京A12345",
            record: record);
        stateManager.SetLastCreatedWeighingRecordId(10);

        await service.TryReWritePlateNumberAsync(stateManager);

        _published.ShouldBeEmpty();
    }

    [Fact]
    public async Task TryReWrite_Should_PublishFallback_WhenUnmatchedAndMatchOnStableEnabled()
    {
        var record = new WeighingRecord(11, 12.5m) { PlateNumber = "京A12345" };

        var (service, stateManager, _) = CreateSut(
            enableMatchOnStable: true,
            plate: "京A12345",
            record: record);
        stateManager.SetLastCreatedWeighingRecordId(11);

        await service.TryReWritePlateNumberAsync(stateManager);

        _published.Count.ShouldBe(1);
        _published[0].WeighingRecordId.ShouldBe(11);
    }

    [Fact]
    public async Task TryReWrite_Should_Publish_WhenMatchOnStableDisabled()
    {
        var record = new WeighingRecord(12, 12.5m) { PlateNumber = "京A12345" };
        record.MatchAsJoin(99, 100); // even if already matched, legacy path still publishes

        var (service, stateManager, _) = CreateSut(
            enableMatchOnStable: false,
            plate: "京A12345",
            record: record);
        stateManager.SetLastCreatedWeighingRecordId(12);

        await service.TryReWritePlateNumberAsync(stateManager);

        _published.Count.ShouldBe(1);
        _published[0].WeighingRecordId.ShouldBe(12);
    }

    [Fact]
    public async Task TryReWrite_Should_NotPublish_WhenSkipWaybillMatching()
    {
        var record = new WeighingRecord(13, 12.5m) { PlateNumber = "京A12345" };

        var (service, stateManager, _) = CreateSut(
            enableMatchOnStable: true,
            plate: "京A12345",
            record: record,
            skipWaybillMatching: true);
        stateManager.SetLastCreatedWeighingRecordId(13);

        await service.TryReWritePlateNumberAsync(stateManager);

        _published.ShouldBeEmpty();
    }

    [Fact]
    public void WeighingConfiguration_EnableMatchOnStable_DefaultsToFalse()
    {
        new WeighingConfiguration().EnableMatchOnStable.ShouldBeFalse();
    }

    private (WeighingRecordService service, WeighingStateManager stateManager, IPlateNumberService plateService)
        CreateSut(
            bool enableMatchOnStable,
            string? plate,
            WeighingRecord? record = null,
            bool skipWaybillMatching = false)
    {
        var settingsService = Substitute.For<ISettingsService>();
        var settings = new SettingsEntity(
            new ScaleSettings(),
            new DocumentScannerConfig(),
            new SystemSettings(),
            [],
            [],
            new WeighingConfiguration
            {
                EnableMatchOnStable = enableMatchOnStable,
                EnablePlateRewrite = true
            },
            new SoundDeviceSettings());
        settingsService.GetSettingsAsync().Returns(Task.FromResult(settings));

        var plateService = Substitute.For<IPlateNumberService>();
        plateService.GetMostFrequentPlateNumber().Returns(plate);

        var repo = Substitute.For<IRepository<WeighingRecord, long>>();
        if (record != null)
            repo.GetAsync(record.Id).Returns(Task.FromResult(record));

        var uow = Substitute.For<IUnitOfWork>();
        uow.CompleteAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var uowManager = Substitute.For<IUnitOfWorkManager>();
        uowManager.Begin(Arg.Any<AbpUnitOfWorkOptions>(), Arg.Any<bool>()).Returns(uow);

        var pipeline = Substitute.For<IWeighingPipelineStrategy>();
        pipeline.ShouldSkipWaybillMatching().Returns(skipWaybillMatching);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var stateManager = new WeighingStateManager(
            _eventBus,
            Substitute.For<ILogger<WeighingStateManager>>());

        var service = new WeighingRecordService(
            repo,
            Substitute.For<IUrbanWeighingExtensionService>(),
            Substitute.For<IRepository<AttachmentFile, int>>(),
            Substitute.For<IRepository<WeighingRecordAttachment, int>>(),
            uowManager,
            settingsService,
            plateService,
            _eventBus,
            Substitute.For<ILogger<WeighingRecordService>>(),
            Substitute.For<IUrbanAnomalyDetector>(),
            configuration,
            pipeline);

        return (service, stateManager, plateService);
    }
}
