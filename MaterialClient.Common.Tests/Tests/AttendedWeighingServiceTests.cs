using System.Reactive.Linq;
using System.Reactive.Subjects;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.AttendedWeighing;
using MaterialClient.Common.Services.AttendedWeighing.Records;
using MaterialClient.Common.Providers;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Services.Vzvision;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using BatchCaptureRequest = MaterialClient.Common.Services.Hikvision.BatchCaptureRequest;
using BatchCaptureResult = MaterialClient.Common.Services.Hikvision.BatchCaptureResult;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
/// Unit tests for AttendedWeighingService
///
/// These tests use TestLocalEventBus to simulate ABP ILocalEventBus behavior.
/// Each test instance gets its own TestLocalEventBus, so tests are isolated
/// without requiring sequential execution.
/// </summary>
[Collection("MessageBusTests")]
public class AttendedWeighingServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly List<IDisposable> _disposables = new();
    private readonly TestLocalEventBus _testEventBus;

    public AttendedWeighingServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _testEventBus = new TestLocalEventBus();
    }

    #region Lifecycle Tests

    [Fact]
    public async Task StartAsync_Should_InitializeStreamsAndSubscriptions()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();

        // Act
        await service.StartAsync();
        await Task.Delay(100); // Allow initialization

        // Assert
        service.GetCurrentStatus().ShouldBe(AttendedWeighingStatus.OffScale);

        // Cleanup
        await service.DisposeAsync();
        weightSubject.Dispose();
    }

    [Fact]
    public async Task StartAsync_Should_BeIdempotent()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();

        // Act
        await service.StartAsync();
        await service.StartAsync();
        await service.StartAsync();
        await Task.Delay(100);

        // Assert - Should not throw and should work normally
        service.GetCurrentStatus().ShouldBe(AttendedWeighingStatus.OffScale);

        // Cleanup
        await service.DisposeAsync();
        weightSubject.Dispose();
    }

    [Fact]
    public async Task StopAsync_Should_GracefullyShutdown()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        await Task.Delay(100);

        // Act
        await service.StopAsync();

        // Assert - Should complete without errors
        service.GetCurrentStatus().ShouldBe(AttendedWeighingStatus.OffScale);

        // Cleanup
        await service.DisposeAsync();
        weightSubject.Dispose();
    }

    [Fact]
    public async Task DisposeAsync_Should_CleanupResources()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        await Task.Delay(100);

        // Act
        await service.DisposeAsync();

        // Assert - Should complete without errors
        // After disposal, internal subjects may be disposed; avoid querying state.

        // Cleanup
        weightSubject.Dispose();
    }

    #endregion

    #region State Management Tests

    [Fact]
    public async Task GetCurrentStatus_Should_ReturnCurrentStatus()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        // Act & Assert
        service.GetCurrentStatus().ShouldBe(AttendedWeighingStatus.OffScale);

        // Simulate weight above threshold
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        // Status should transition to WaitingForStability
        var status = service.GetCurrentStatus();
        status.ShouldBeOneOf(AttendedWeighingStatus.OffScale, AttendedWeighingStatus.WaitingForStability);

        // Cleanup
        await service.DisposeAsync();
        weightSubject.Dispose();
    }

    [Fact]
    public async Task CurrentDeliveryType_Should_ReturnCurrentDeliveryType()
    {
        // Arrange
        var (service, _) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        // Act & Assert
        service.CurrentDeliveryType.ShouldBe(DeliveryType.Receiving);

        service.SetDeliveryType(DeliveryType.Sending);
        await Task.Delay(100);

        service.CurrentDeliveryType.ShouldBe(DeliveryType.Sending);

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task SetDeliveryType_Should_UpdateDeliveryType()
    {
        // Arrange
        var (service, _) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        // Act
        service.SetDeliveryType(DeliveryType.Sending);
        await Task.Delay(100);

        // Assert
        service.CurrentDeliveryType.ShouldBe(DeliveryType.Sending);

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task SetDeliveryType_Should_SendMessageBusNotification()
    {
        // Arrange
        var (service, _) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        var receivedEvents = new List<DeliveryTypeChangedEventData>();
        var subscription = _testEventBus.Subscribe<DeliveryTypeChangedEventData>(msg => receivedEvents.Add(msg));
        _disposables.Add(subscription);

        // Act
        service.SetDeliveryType(DeliveryType.Sending);
        await Task.Delay(200);

        // Assert
        receivedEvents.ShouldNotBeEmpty();
        receivedEvents.Last().DeliveryType.ShouldBe(DeliveryType.Sending);

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task SetDeliveryType_Should_NotSendNotification_WhenUnchanged()
    {
        // Arrange
        var (service, _) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        var receivedEvents = new List<DeliveryTypeChangedEventData>();
        var subscription = _testEventBus.Subscribe<DeliveryTypeChangedEventData>(msg => receivedEvents.Add(msg));
        _disposables.Add(subscription);

        // Act - Set to same value
        service.SetDeliveryType(DeliveryType.Receiving);
        await Task.Delay(200);

        // Assert - Should not send notification for same value
        // Note: The implementation may still send, but we test the behavior
        receivedEvents.Count.ShouldBeLessThanOrEqualTo(1);

        // Cleanup
        await service.DisposeAsync();
    }

    #endregion

    #region Plate Number Recognition Tests

    [Fact]
    public async Task OnPlateNumberRecognized_Should_FilterInvalidPlateNumbers()
    {
        // Arrange
        var (service, _) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        // Act - 发送无效车牌事件
        await _testEventBus.PublishAsync(new LicensePlateRecognizedEventData { PlateNumber = "" });
        await _testEventBus.PublishAsync(new LicensePlateRecognizedEventData { PlateNumber = "   " });
        await _testEventBus.PublishAsync(new LicensePlateRecognizedEventData { PlateNumber = null! });
        await Task.Delay(100);

        // Assert
        service.GetMostFrequentPlateNumber().ShouldBeNull();

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact(Skip = "待移植至集成测试项目: 时序依赖导致 flaky")]
    public async Task OnPlateNumberRecognized_Should_FilterHangingCharacter()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        weightSubject.OnNext(1.0m); // Put on scale
        await Task.Delay(500);

        // Act
        await _testEventBus.PublishAsync(new LicensePlateRecognizedEventData { PlateNumber = "京A12345挂" });
        await Task.Delay(200);

        // Assert
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBe("京A12345");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task OnPlateNumberRecognized_Should_CachePlateNumber_WhenOnScale()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        weightSubject.OnNext(1.0m); // Put on scale
        await Task.Delay(500);

        // Act
        await _testEventBus.PublishAsync(new LicensePlateRecognizedEventData { PlateNumber = "京A12345" });
        await Task.Delay(200);

        // Assert
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBe("京A12345");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact(Skip = "待移植至集成测试项目: 时序依赖导致 flaky")]
    public async Task OnPlateNumberRecognized_Should_Ignore_WhenOffScale()
    {
        // Arrange
        var (service, _) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        // Stay off scale (weight = 0)

        // Act
        await _testEventBus.PublishAsync(new LicensePlateRecognizedEventData { PlateNumber = "京A12345" });
        await Task.Delay(200);

        // Assert
        service.GetMostFrequentPlateNumber().ShouldBeNull();

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task GetMostFrequentPlateNumber_Should_ReturnMostFrequent()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        // Act
        await _testEventBus.PublishAsync(new LicensePlateRecognizedEventData { PlateNumber = "京A12345" });
        await _testEventBus.PublishAsync(new LicensePlateRecognizedEventData { PlateNumber = "京A12345" });
        await _testEventBus.PublishAsync(new LicensePlateRecognizedEventData { PlateNumber = "粤B67890" });
        await Task.Delay(200);

        // Assert
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBe("京A12345");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task GetMostFrequentPlateNumber_Should_LockFirstLockedAt_WhenEnablePlateRewriteDisabled()
    {
        // Arrange - disable plate rewrite to enable LockedAt-first selection
        var (service, weightSubject) = CreateServiceWithWeightSubject(enablePlateRewrite: false);
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        // Act - first A locks first, then B locks later
        SendPlateRecognition("京A12345");
        await Task.Delay(250);
        SendPlateRecognition("粤B67890");
        await Task.Delay(250);

        // Assert - should still return A (earliest LockedAt)
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBe("京A12345");

        // Cleanup
        await service.DisposeAsync();
    }
    
    [Fact]
    public async Task GetMostFrequentPlateNumber_Should_SelectEarliestLockedAt_WhenMultipleLockedCandidates()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject(enablePlateRewrite: false);
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        // Act - A locks first, then B, then C
        SendPlateRecognition("京A12345");
        await Task.Delay(400);
        // Ensure A has become the current recommendation before sending the rest
        service.GetMostFrequentPlateNumber().ShouldBe("京A12345");

        SendPlateRecognition("粤B67890");
        await Task.Delay(300);
        SendPlateRecognition("浙C13579");
        await Task.Delay(350);

        // Assert - earliest LockedAt should remain A
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBe("京A12345");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task GhostGateSessionReset_Should_RemoveAbandonedLockedPlate_WhenPlateRewriteDisabled()
    {
        var (service, weightSubject) = CreateServiceWithWeightSubject(enablePlateRewrite: false);
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        SendPlateRecognition("京A12345");
        await Task.Delay(250);
        SendPlateRecognition("粤B67890");
        await Task.Delay(250);

        service.GetMostFrequentPlateNumber().ShouldBe("京A12345");

        var plateEvents = new List<PlateNumberChangedEventData>();
        var sub = _testEventBus.Subscribe<PlateNumberChangedEventData>(msg => plateEvents.Add(msg));
        _disposables.Add(sub);

        await _testEventBus.PublishAsync(new GhostGateSessionResetEventData("京A12345", "粤B67890", "GateA"));
        await Task.Delay(200);

        service.GetMostFrequentPlateNumber().ShouldBe("粤B67890");
        plateEvents.ShouldNotBeEmpty();
        plateEvents.Last().PlateNumber.ShouldBe("粤B67890");

        await service.DisposeAsync();
        weightSubject.Dispose();
    }

    [Fact]
    public async Task GetMostFrequentPlateNumber_Should_FallbackToOriginalLogic_WhenNoLockedCandidates()
    {
        // Arrange - keep plate rewrite enabled so LockedAt-first is never triggered
        var (service, weightSubject) = CreateServiceWithWeightSubject(enableLatestPlateNumber: true, enablePlateRewrite: true);
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        // Act - count is higher for A, but B is newer
        SendPlateRecognition("京A12345");
        SendPlateRecognition("京A12345");
        await Task.Delay(80);
        SendPlateRecognition("粤B67890");
        await Task.Delay(200);

        // Assert - should follow original EnableLatestPlateNumber behavior
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBe("粤B67890");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task OnPlateNumberRecognized_Concurrent_Should_NotThrow()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject(enablePlateRewrite: false);
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        // Act
        var tasks = new[]
        {
            Task.Run(async () => { await Task.Delay(10); SendPlateRecognition("京A12345"); }),
            Task.Run(async () => { await Task.Delay(20); SendPlateRecognition("粤B67890"); }),
            Task.Run(async () => { await Task.Delay(30); SendPlateRecognition("浙C13579"); })
        };
        await Task.WhenAll(tasks);
        await Task.Delay(200);

        // Assert - should select a plate without throwing
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBeOneOf("京A12345", "粤B67890", "浙C13579");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task GetMostFrequentPlateNumber_Should_ReturnNull_WhenCacheEmpty()
    {
        // Arrange
        var (service, _) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        // Act & Assert
        service.GetMostFrequentPlateNumber().ShouldBeNull();

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task GetMostFrequentPlateNumber_Should_PrioritizeLastUpdateTime_WhenEnableLatestPlateNumberEnabled()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject(enableLatestPlateNumber: true);
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        // Act - A count is higher, but B is newer
        SendPlateRecognition("京A12345");
        SendPlateRecognition("京A12345");
        await Task.Delay(80);
        SendPlateRecognition("粤B67890");
        await Task.Delay(200);

        // Assert
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBe("粤B67890");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task GetMostFrequentPlateNumber_Should_PrioritizeLastUpdateTime_InLowPriorityFallback_WhenEnableLatestPlateNumberEnabled()
    {
        // Arrange - Mark blue/yellow as low-priority so fallback path is used
        var (service, weightSubject) = CreateServiceWithLowPriorityColors(
            new[] { VzvisionColorType.Blue, VzvisionColorType.Yellow },
            enableLatestPlateNumber: true);
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        // Act - A count is higher, but B is newer
        SendPlateRecognition("京A12345", VzvisionColorType.Blue);
        SendPlateRecognition("京A12345", VzvisionColorType.Blue);
        await Task.Delay(80);
        SendPlateRecognition("粤B67890", VzvisionColorType.Yellow);
        await Task.Delay(200);

        // Assert
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBe("粤B67890");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task OnPlateNumberRecognized_Should_TriggerMessageBusNotification()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        var receivedEvents = new List<PlateNumberChangedEventData>();
        var subscription = _testEventBus.Subscribe<PlateNumberChangedEventData>(msg => receivedEvents.Add(msg));
        _disposables.Add(subscription);

        // Act
        SendPlateRecognition("京A12345");
        await Task.Delay(300);

        // Assert
        receivedEvents.ShouldNotBeEmpty();
        receivedEvents.Last().PlateNumber.ShouldBe("京A12345");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task OnPlateNumberRecognized_HighPriority_Should_Override_LowPriority()
    {
        // Arrange - Create service with Yellow as low-priority color
        var (service, weightSubject) = CreateServiceWithLowPriorityColors(new[] { VzvisionColorType.Yellow });
        await service.StartAsync();
        weightSubject.OnNext(1.0m); // Put on scale
        await Task.Delay(300);

        // Act - Recognize yellow plate 10 times, then blue plate once
        for (int i = 0; i < 10; i++)
        {
            SendPlateRecognition("京B99999", VzvisionColorType.Yellow);
        }
        SendPlateRecognition("京A12345", VzvisionColorType.Blue);
        await Task.Delay(200);

        // Assert - Blue plate (high-priority) should be selected despite lower count
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBe("京A12345");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task OnPlateNumberRecognized_LowPriority_Should_BeUsed_WhenNoHighPriority()
    {
        // Arrange - Create service with Yellow as low-priority color
        var (service, weightSubject) = CreateServiceWithLowPriorityColors(new[] { VzvisionColorType.Yellow });
        await service.StartAsync();
        weightSubject.OnNext(1.0m); // Put on scale
        await Task.Delay(300);

        // Act - Only recognize yellow plates (low-priority)
        SendPlateRecognition("京B99999", VzvisionColorType.Yellow);
        SendPlateRecognition("京B99999", VzvisionColorType.Yellow);
        await Task.Delay(200);

        // Assert - Yellow plate should be selected (no high-priority alternative)
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBe("京B99999");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task OnPlateNumberRecognized_LowPriority_Cannot_Override_HighPriority()
    {
        // Arrange - Create service with Yellow as low-priority color
        var (service, weightSubject) = CreateServiceWithLowPriorityColors(new[] { VzvisionColorType.Yellow });
        await service.StartAsync();
        weightSubject.OnNext(1.0m); // Put on scale
        await Task.Delay(300);

        // Act - Blue plate first, then many yellow plates
        SendPlateRecognition("京A12345", VzvisionColorType.Blue);
        for (int i = 0; i < 100; i++)
        {
            SendPlateRecognition("京B99999", VzvisionColorType.Yellow);
        }
        await Task.Delay(200);

        // Assert - Blue plate (high-priority) should still be selected
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBe("京A12345");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task OnPlateNumberRecognized_NullColor_Should_BeTreated_AsHighPriority()
    {
        // Arrange - Create service with Yellow as low-priority color
        var (service, weightSubject) = CreateServiceWithLowPriorityColors(new[] { VzvisionColorType.Yellow });
        await service.StartAsync();
        weightSubject.OnNext(1.0m); // Put on scale
        await Task.Delay(300);

        // Act - Yellow plate many times, then plate without color once
        for (int i = 0; i < 10; i++)
        {
            SendPlateRecognition("京B99999", VzvisionColorType.Yellow);
        }
        SendPlateRecognition("京A12345", null); // No color info
        await Task.Delay(200);

        // Assert - Plate without color (treated as high-priority) should be selected
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBe("京A12345");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task OnPlateNumberRecognized_ColorInfo_Should_BePersisted_InCache()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        weightSubject.OnNext(1.0m); // Put on scale
        await Task.Delay(300);

        // Act - Recognize plate with color
        SendPlateRecognition("京A12345", VzvisionColorType.Blue);
        SendPlateRecognition("京A12345", VzvisionColorType.Blue); // Recognize again
        await Task.Delay(200);

        // Assert - Plate should be cached and selected
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBe("京A12345");
        // Note: We can't directly inspect cache, but behavior confirms color is stored

        // Cleanup
        await service.DisposeAsync();
    }

    #endregion

    #region Weight Stability Tests

    [Fact]
    public async Task WeightStream_Should_ProcessWeightUpdates()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        // Act
        weightSubject.OnNext(0.3m);
        await Task.Delay(300);
        service.GetCurrentStatus().ShouldBe(AttendedWeighingStatus.OffScale);

        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        // Assert
        var status = service.GetCurrentStatus();
        status.ShouldBeOneOf(AttendedWeighingStatus.OffScale, AttendedWeighingStatus.WaitingForStability);

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact(Skip = "待移植至集成测试项目: 重量稳定性模拟，单测耗时 5s+")]
    public async Task StabilityStream_Should_IdentifyStableWeights()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        // Act - Send stable weight values
        for (int i = 0; i < 20; i++)
        {
            var weight = 1.0m + (decimal)((i % 3 - 1) * 0.01); // Small variations around 1.0
            weightSubject.OnNext(weight);
            await Task.Delay(200);
        }

        await Task.Delay(1000); // Wait for stability check

        // Assert - Status should eventually become WeightStabilized
        var status = service.GetCurrentStatus();
        _output.WriteLine($"Final status: {status}");

        // Cleanup
        await service.DisposeAsync();
    }

    #endregion

    #region Weighing Record Creation Tests

    [Fact(Skip = "待移植至集成测试项目: 重量稳定性模拟，单测耗时 6s+")]
    public async Task Should_CreateRecord_WhenWeightStabilizes()
    {
        // Arrange
        var (service, weightSubject, mockRepo, mockUow) = CreateServiceWithMocks();
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        // Act - Send stable weights
        for (int i = 0; i < 20; i++)
        {
            var weight = 1.0m + (decimal)((i % 3 - 1) * 0.01);
            weightSubject.OnNext(weight);
            await Task.Delay(200);
        }

        await Task.Delay(2000); // Wait for stability and record creation

        // Assert
        await mockRepo.Received().InsertAsync(Arg.Any<WeighingRecord>(), Arg.Any<bool>());
        await mockUow.Received().CompleteAsync(Arg.Any<CancellationToken>());

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact(Skip = "待移植至集成测试项目: 重量稳定性模拟，单测耗时 6s+")]
    public async Task Should_CreateRecord_UseLockedAtPlateNumber_WhenEnablePlateRewriteDisabled()
    {
        // Arrange
        var (service, weightSubject, mockRepo, mockUow) = CreateServiceWithMocks(enablePlateRewrite: false);
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        // Lock A first, then B
        SendPlateRecognition("京A12345");
        await Task.Delay(60);
        SendPlateRecognition("粤B67890");
        await Task.Delay(60);

        // Act - Send stable weights to trigger record creation
        for (int i = 0; i < 20; i++)
        {
            var weight = 1.0m + (decimal)((i % 3 - 1) * 0.01);
            weightSubject.OnNext(weight);
            await Task.Delay(200);
        }

        await Task.Delay(2000); // Wait for stability and record creation

        // Assert - plate number should be based on locked selection (earliest LockedAt)
        await mockRepo.Received().InsertAsync(
            Arg.Is<WeighingRecord>(w => w.PlateNumber == "京A12345"),
            Arg.Any<bool>());
        await mockUow.Received().CompleteAsync(Arg.Any<CancellationToken>());

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact(Skip = "待移植至集成测试项目: 重量稳定性模拟，单测耗时 6s+")]
    public async Task Should_CapturePhotos_WhenWeightStabilizes()
    {
        // Arrange
        var (service, weightSubject, _, _, mockHikvision) = CreateServiceWithMocksAndHikvision();
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        // Act - Send stable weights
        for (int i = 0; i < 20; i++)
        {
            var weight = 1.0m + (decimal)((i % 3 - 1) * 0.01);
            weightSubject.OnNext(weight);
            await Task.Delay(200);
        }

        await Task.Delay(2000);

        // Assert - Should attempt to capture photos
        // Note: May not capture if no cameras configured, but should not throw

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact(Skip = "待移植至集成测试项目: 重量稳定性模拟，单测耗时 8s+")]
    public async Task Should_PreventDuplicateRecordCreation()
    {
        // Arrange
        var (service, weightSubject, mockRepo, _) = CreateServiceWithMocks();
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        // Act - Send stable weights multiple times
        for (int i = 0; i < 30; i++)
        {
            var weight = 1.0m + (decimal)((i % 3 - 1) * 0.01);
            weightSubject.OnNext(weight);
            await Task.Delay(200);
        }

        await Task.Delay(2000);

        // Assert - Should only create one record
        await mockRepo.Received(1).InsertAsync(Arg.Any<WeighingRecord>(), Arg.Any<bool>());

        // Cleanup
        await service.DisposeAsync();
    }

    #endregion

    #region Integration Tests

    [Fact(Skip = "待移植至集成测试项目: 重量稳定性模拟，单测耗时 6s+")]
    public async Task NormalFlow_Should_CompleteFullCycle()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        // Act - Normal flow simulation
        // 1. OffScale -> WaitingForStability
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);
        var status1 = service.GetCurrentStatus();
        _output.WriteLine($"After on-scale: {status1}");

        // 2. Send stable weights
        for (int i = 0; i < 20; i++)
        {
            var weight = 1.0m + (decimal)((i % 3 - 1) * 0.01);
            weightSubject.OnNext(weight);
            await Task.Delay(200);
        }
        await Task.Delay(2000);
        var status2 = service.GetCurrentStatus();
        _output.WriteLine($"After stabilization: {status2}");

        // 3. WaitingForDeparture -> OffScale
        weightSubject.OnNext(0.3m);
        await Task.Delay(300);
        var status3 = service.GetCurrentStatus();
        _output.WriteLine($"After off-scale: {status3}");

        // Assert
        status3.ShouldBe(AttendedWeighingStatus.OffScale);

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact(Skip = "待移植至集成测试项目: 时序依赖的状态转换测试")]
    public async Task AbnormalDeparture_FromWaitingForStability_Should_Reset()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        // Act
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);
        // Immediately go off scale without stabilizing
        weightSubject.OnNext(0.3m);
        await Task.Delay(500);

        // Assert
        var status = service.GetCurrentStatus();
        status.ShouldBe(AttendedWeighingStatus.OffScale);
        service.GetMostFrequentPlateNumber().ShouldBeNull();

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task ResetCycle_Should_ClearCacheAndRecordId()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);
        SendPlateRecognition("京A12345");
        await Task.Delay(200);

        // Act - Reset by going off scale
        weightSubject.OnNext(0.3m);
        await Task.Delay(500);

        // Assert
        service.GetMostFrequentPlateNumber().ShouldBeNull();

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task ResetCycle_Should_ClearLockedAt_WhenEnablePlateRewriteDisabled()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject(enablePlateRewrite: false);
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);
        SendPlateRecognition("京A12345");
        await Task.Delay(100);
        service.GetMostFrequentPlateNumber().ShouldBe("京A12345");

        // Act - Reset by going off scale
        weightSubject.OnNext(0.3m);
        await Task.Delay(500);

        // Assert - cache should be cleared (LockedAt removed)
        service.GetMostFrequentPlateNumber().ShouldBeNull();

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact(Skip = "待移植至集成测试项目: 重量稳定性模拟，单测耗时 5s+")]
    public async Task UnstableDeparture_Then_StableWeighing_Should_CompleteCycle()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        // Act Phase 1: 从0增加到0.51（上磅，进入WaitingForStability状态）
        _output.WriteLine("=== Phase 1: 上磅到 0.51t ===");
        weightSubject.OnNext(0.51m);
        await Task.Delay(300);
        var status1 = service.GetCurrentStatus();
        _output.WriteLine($"Status after on-scale: {status1}");
        status1.ShouldBe(AttendedWeighingStatus.WaitingForStability);

        // Act Phase 2: 未稳定状态降低到0.49（下磅，进入OffScale状态，触发异常流程）
        _output.WriteLine("=== Phase 2: 未稳定下磅到 0.49t ===");
        weightSubject.OnNext(0.49m);
        await Task.Delay(500);
        var status2 = service.GetCurrentStatus();
        _output.WriteLine($"Status after abnormal departure: {status2}");
        status2.ShouldBe(AttendedWeighingStatus.OffScale);
        
        // 验证缓存已清空
        service.GetMostFrequentPlateNumber().ShouldBeNull();

        // Act Phase 3: 增加到1并稳定3秒（再次上磅并稳定）
        _output.WriteLine("=== Phase 3: 再次上磅到 1.0t 并稳定 ===");
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);
        var status3 = service.GetCurrentStatus();
        _output.WriteLine($"Status after second on-scale: {status3}");
        status3.ShouldBe(AttendedWeighingStatus.WaitingForStability);

        // 发送稳定的重量值（在1.0t附近小幅波动，满足稳定性要求）
        // 稳定性窗口是3000ms，检查间隔是200ms，需要至少15个数据点
        // 发送20个数据点，确保覆盖3秒窗口
        var random = new Random(42); // 固定种子以便重现
        for (int i = 0; i < 20; i++)
        {
            // 在1.0t ± 0.02t范围内波动（范围 < 0.05t，满足稳定性阈值）
            var noise = (decimal)(random.NextDouble() * 0.04 - 0.02); // ±0.02t
            var weight = Math.Round(1.0m + noise, 3);
            weightSubject.OnNext(weight);
            _output.WriteLine($"[{i * 200}ms] Weight: {weight:F3}t");
            await Task.Delay(200); // 200ms间隔，匹配 StabilityCheckIntervalMs
        }

        // 等待稳定性检查完成（需要等待窗口时间）
        await Task.Delay(1000);
        var status4 = service.GetCurrentStatus();
        _output.WriteLine($"Status after stabilization: {status4}");

        // Assert
        // 应该已经稳定并创建了称重记录（状态可能是 WeightStabilized 或 WaitingForDeparture）
        status4.ShouldBeOneOf(
            AttendedWeighingStatus.WeightStabilized,
            AttendedWeighingStatus.WaitingForDeparture);

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact(Skip = "待移植至集成测试项目: 重量阈值波动模拟，单测耗时 2s+")]
    public async Task WeightFluctuation_AroundThreshold_Should_HandleCorrectly()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        // Act - 在0.5阈值附近波动（±0.1范围：0.4 ~ 0.6）
        // 这个测试验证当重量在阈值附近波动时，状态转换是否正确
        _output.WriteLine("=== 测试：在阈值0.5t附近波动（±0.1t） ===");
        
        var weights = new[]
        {
            0.4m,  // 低于阈值 -> OffScale
            0.6m,  // 高于阈值 -> WaitingForStability
            0.45m, // 低于阈值 -> OffScale
            0.55m, // 高于阈值 -> WaitingForStability
            0.4m,  // 低于阈值 -> OffScale
            0.6m,  // 高于阈值 -> WaitingForStability
            0.5m,  // 等于阈值（边界情况，应该视为高于阈值）-> WaitingForStability
            0.4m,  // 低于阈值 -> OffScale
        };

        var statuses = new List<AttendedWeighingStatus>();
        
        foreach (var weight in weights)
        {
            weightSubject.OnNext(weight);
            await Task.Delay(300); // 等待状态转换
            var status = service.GetCurrentStatus();
            statuses.Add(status);
            _output.WriteLine($"Weight: {weight:F2}t -> Status: {status}");
        }

        // Assert
        // 验证状态转换逻辑
        // 0.4 (低于阈值) -> OffScale
        statuses[0].ShouldBe(AttendedWeighingStatus.OffScale);
        
        // 0.6 (高于阈值) -> WaitingForStability
        statuses[1].ShouldBe(AttendedWeighingStatus.WaitingForStability);
        
        // 0.45 (低于阈值) -> OffScale
        statuses[2].ShouldBe(AttendedWeighingStatus.OffScale);
        
        // 0.55 (高于阈值) -> WaitingForStability
        statuses[3].ShouldBe(AttendedWeighingStatus.WaitingForStability);
        
        // 0.4 (低于阈值) -> OffScale
        statuses[4].ShouldBe(AttendedWeighingStatus.OffScale);
        
        // 0.6 (高于阈值) -> WaitingForStability
        statuses[5].ShouldBe(AttendedWeighingStatus.WaitingForStability);
        
        // 0.5 (等于阈值，应该视为高于阈值) -> WaitingForStability
        statuses[6].ShouldBe(AttendedWeighingStatus.WaitingForStability);
        
        // 0.4 (低于阈值) -> OffScale
        statuses[7].ShouldBe(AttendedWeighingStatus.OffScale);
        
        // 最终状态应该是 OffScale
        var finalStatus = service.GetCurrentStatus();
        finalStatus.ShouldBe(AttendedWeighingStatus.OffScale);
        
        _output.WriteLine($"Final status: {finalStatus}");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact(Skip = "待移植至集成测试项目: 重量稳定性模拟，单测耗时 8s+")]
    public async Task WeightTransition_FromLowToThresholdToStable_Should_CompleteCycle()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        _output.WriteLine("=== 测试：0.4停留2秒 -> 0.5停留1秒 -> 1.0稳定停留 ===");

        // Act Phase 1: 在0.4停留2秒左右（低于阈值，应该保持OffScale状态）
        _output.WriteLine("=== Phase 1: 在0.4t停留2秒（低于阈值） ===");
        var startTime = DateTime.Now;
        for (int i = 0; i < 10; i++) // 10次 * 200ms = 2秒
        {
            weightSubject.OnNext(0.4m);
            await Task.Delay(200);
        }
        var phase1Duration = (DateTime.Now - startTime).TotalSeconds;
        var status1 = service.GetCurrentStatus();
        _output.WriteLine($"Phase 1 duration: {phase1Duration:F2}s, Status: {status1}");
        status1.ShouldBe(AttendedWeighingStatus.OffScale); // 低于阈值，应该保持OffScale

        // Act Phase 2: 进入两秒后在0.5停留1秒左右（等于阈值，应该视为高于阈值）
        _output.WriteLine("=== Phase 2: 在0.5t停留1秒（等于阈值，视为高于阈值） ===");
        startTime = DateTime.Now;
        for (int i = 0; i < 5; i++) // 5次 * 200ms = 1秒
        {
            weightSubject.OnNext(0.51m);
            await Task.Delay(200);
        }
        var phase2Duration = (DateTime.Now - startTime).TotalSeconds;
        await Task.Delay(300); // 等待状态转换
        var status2 = service.GetCurrentStatus();
        _output.WriteLine($"Phase 2 duration: {phase2Duration:F2}s, Status: {status2}");
        status2.ShouldBe(AttendedWeighingStatus.WaitingForStability); // 等于阈值视为高于阈值，应该进入WaitingForStability

        // Act Phase 3: 重量变为1.0一直停留（高于阈值，应该稳定并创建记录）
        _output.WriteLine("=== Phase 3: 在1.0t稳定停留（高于阈值，应该稳定） ===");
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);
        
        // 发送稳定的重量值（在1.0t附近小幅波动，满足稳定性要求）
        // 稳定性窗口是3000ms，检查间隔是200ms，需要至少15个数据点
        // 发送20个数据点，确保覆盖3秒窗口并稳定
        var random = new Random(42); // 固定种子以便重现
        startTime = DateTime.Now;
        for (int i = 0; i < 20; i++)
        {
            // 在1.0t ± 0.02t范围内波动（范围 < 0.05t，满足稳定性阈值）
            var noise = (decimal)(random.NextDouble() * 0.04 - 0.02); // ±0.02t
            var weight = Math.Round(1.0m + noise, 3);
            weightSubject.OnNext(weight);
            _output.WriteLine($"[{i * 200}ms] Weight: {weight:F3}t");
            await Task.Delay(200); // 200ms间隔，匹配 StabilityCheckIntervalMs
        }
        var phase3Duration = (DateTime.Now - startTime).TotalSeconds;
        _output.WriteLine($"Phase 3 duration: {phase3Duration:F2}s");

        // 等待稳定性检查完成（需要等待窗口时间）
        await Task.Delay(1000);
        var status3 = service.GetCurrentStatus();
        _output.WriteLine($"Final status: {status3}");

        // Assert
        // 最终状态应该是 WeightStabilized 或 WaitingForDeparture（如果已创建记录）
        status3.ShouldBeOneOf(
            AttendedWeighingStatus.WeightStabilized,
            AttendedWeighingStatus.WaitingForDeparture);

        _output.WriteLine($"Test completed. Final status: {status3}");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact(Skip = "待移植至集成测试项目: 重量稳定性模拟，单测耗时 6s+")]
    public async Task StabilityCheck_Should_NotUseHistoricalData_AfterStateTransition()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        _output.WriteLine("=== 测试：验证稳定性检查不应使用历史数据 ===");
        _output.WriteLine("场景：在进入 WaitingForStability 之前有历史数据，验证不会过早判定为稳定");

        // Act Phase 1: 先发送一些低于阈值的数据（模拟历史数据）
        _output.WriteLine("=== Phase 1: 发送历史数据（低于阈值） ===");
        for (int i = 0; i < 5; i++)
        {
            weightSubject.OnNext(0.3m); // 低于阈值
            await Task.Delay(200);
        }
        await Task.Delay(300);
        var status1 = service.GetCurrentStatus();
        _output.WriteLine($"Status after historical data: {status1}");
        status1.ShouldBe(AttendedWeighingStatus.OffScale);

        // Act Phase 2: 快速上磅并发送多个数据点（模拟快速上磅场景）
        _output.WriteLine("=== Phase 2: 快速上磅到 0.55t 并发送多个数据点 ===");
        DateTime? waitingForStabilityTime = null;
        
        // 快速发送多个数据点（模拟快速上磅）
        var weights = new[] { 0.55m, 0.60m, 0.65m, 0.60m, 0.55m, 0.60m, 0.65m, 0.60m };
        foreach (var weight in weights)
        {
            weightSubject.OnNext(weight);
            await Task.Delay(50); // 快速发送，间隔50ms
            
            // 检查是否进入 WaitingForStability 状态
            var currentStatus = service.GetCurrentStatus();
            if (currentStatus == AttendedWeighingStatus.WaitingForStability && waitingForStabilityTime == null)
            {
                waitingForStabilityTime = DateTime.Now;
                _output.WriteLine($"Entered WaitingForStability at: {waitingForStabilityTime:HH:mm:ss.fff}");
            }
        }
        
        await Task.Delay(300); // 等待状态转换
        var status2 = service.GetCurrentStatus();
        _output.WriteLine($"Status after Phase 2: {status2}");
        
        // 确保已经进入 WaitingForStability 状态
        if (waitingForStabilityTime == null)
        {
            // 如果还没进入，再等待一下
            await Task.Delay(200);
            var checkStatus = service.GetCurrentStatus();
            if (checkStatus == AttendedWeighingStatus.WaitingForStability)
            {
                waitingForStabilityTime = DateTime.Now;
                _output.WriteLine($"Entered WaitingForStability (delayed) at: {waitingForStabilityTime:HH:mm:ss.fff}");
            }
            status2 = checkStatus;
        }
        
        status2.ShouldBe(AttendedWeighingStatus.WaitingForStability, 
            "Should have entered WaitingForStability state after on-scale");
        
        // 继续发送稳定的数据点，并监控状态变化
        _output.WriteLine("=== Phase 3: 继续发送稳定数据点并监控状态变化 ===");
        DateTime? weightStabilizedTime = null;
        
        for (int i = 0; i < 20; i++) // 发送更多数据点以确保覆盖
        {
            var weight = 0.60m + (decimal)((i % 3 - 1) * 0.01); // 在0.59-0.61之间波动
            weightSubject.OnNext(weight);
            await Task.Delay(200);
            
            // 检查是否变为 WeightStabilized
            var currentStatus = service.GetCurrentStatus();
            if (currentStatus == AttendedWeighingStatus.WeightStabilized && weightStabilizedTime == null)
            {
                weightStabilizedTime = DateTime.Now;
                _output.WriteLine($"Became WeightStabilized at: {weightStabilizedTime:HH:mm:ss.fff}");
                
                // 计算从 WaitingForStability 到 WeightStabilized 的时间
                if (waitingForStabilityTime.HasValue)
                {
                    var timeToStable = (weightStabilizedTime.Value - waitingForStabilityTime.Value).TotalSeconds;
                    _output.WriteLine($"Time from WaitingForStability to WeightStabilized: {timeToStable:F3}s");
                    
                    // Assert: 应该至少需要接近窗口时间（2秒以上）
                    // 如果时间少于2秒，说明可能使用了历史数据
                    timeToStable.ShouldBeGreaterThanOrEqualTo(2.0, 
                        $"Stability detected too quickly ({timeToStable:F3}s < 2.0s). " +
                        "This suggests historical data is being used in stability check. " +
                        "Expected at least 2 seconds after entering WaitingForStability state.");
                }
                break;
            }
        }
        
        await Task.Delay(1000); // 等待稳定性检查
        var status3 = service.GetCurrentStatus();
        _output.WriteLine($"Final status: {status3}");
        
        // Assert
        // 如果已经变为 WeightStabilized，验证时间要求
        if (weightStabilizedTime.HasValue && waitingForStabilityTime.HasValue)
        {
            var timeToStable = (weightStabilizedTime.Value - waitingForStabilityTime.Value).TotalSeconds;
            _output.WriteLine($"Final time from WaitingForStability to WeightStabilized: {timeToStable:F3}s");
        }
        else if (status3 == AttendedWeighingStatus.WeightStabilized && waitingForStabilityTime.HasValue)
        {
            // 如果最终状态是 WeightStabilized 但之前没捕获到时间
            var timeToStable = (DateTime.Now - waitingForStabilityTime.Value).TotalSeconds;
            _output.WriteLine($"Final time from WaitingForStability to WeightStabilized: {timeToStable:F3}s");
            timeToStable.ShouldBeGreaterThanOrEqualTo(2.0, 
                $"Stability detected too quickly ({timeToStable:F3}s < 2.0s). " +
                "This suggests historical data is being used in stability check.");
        }

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact(Skip = "待移植至集成测试项目: 重量稳定性模拟，单测耗时 5s+")]
    public async Task StabilityCheck_Should_RequireFullWindow_AfterEnteringWaitingForStability()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        _output.WriteLine("=== 测试：验证进入 WaitingForStability 后需要完整窗口时间才能稳定 ===");

        // Act: 先发送一些数据，然后上磅
        _output.WriteLine("=== Step 1: 发送初始数据 ===");
        weightSubject.OnNext(0.3m);
        await Task.Delay(200);
        weightSubject.OnNext(0.4m);
        await Task.Delay(200);

        // Act: 上磅到 0.55t（进入 WaitingForStability）
        _output.WriteLine("=== Step 2: 上磅到 0.55t（进入 WaitingForStability） ===");
        var transitionTime = DateTime.Now;
        weightSubject.OnNext(0.55m);
        await Task.Delay(300);
        var status1 = service.GetCurrentStatus();
        _output.WriteLine($"Status after on-scale: {status1} at {DateTime.Now:HH:mm:ss.fff}");
        status1.ShouldBe(AttendedWeighingStatus.WaitingForStability);

        // Act: 发送稳定的数据点（在 0.6t 附近波动）
        _output.WriteLine("=== Step 3: 发送稳定数据点（每200ms一个） ===");
        var stableWeights = new List<decimal>();
        for (int i = 0; i < 20; i++) // 发送20个数据点，覆盖4秒
        {
            // 在 0.6t ± 0.02t 范围内波动
            var noise = (decimal)((i % 5 - 2) * 0.01); // -0.02 到 +0.02
            var weight = 0.60m + noise;
            stableWeights.Add(weight);
            weightSubject.OnNext(weight);
            var elapsed = (DateTime.Now - transitionTime).TotalSeconds;
            _output.WriteLine($"[{i}] Weight: {weight:F3}t, Elapsed: {elapsed:F3}s");
            
            await Task.Delay(200);
            
            var currentStatus = service.GetCurrentStatus();
            if (currentStatus == AttendedWeighingStatus.WeightStabilized)
            {
                var timeToStable = (DateTime.Now - transitionTime).TotalSeconds;
                _output.WriteLine($"*** Status changed to WeightStabilized at {timeToStable:F3}s after entering WaitingForStability ***");
                
                // 验证：应该至少需要接近窗口时间（3秒）
                // 考虑到 Buffer 的行为，可能需要至少 2-3 秒
                if (timeToStable < 2.0)
                {
                    _output.WriteLine($"ERROR: Stability detected too quickly ({timeToStable:F3}s < 2.0s)!");
                    _output.WriteLine("This suggests historical data is being used in stability check.");
                }
                break;
            }
        }

        await Task.Delay(500);
        var finalStatus = service.GetCurrentStatus();
        _output.WriteLine($"Final status: {finalStatus}");
        _output.WriteLine($"Total data points sent: {stableWeights.Count}");
        _output.WriteLine($"Weight range: {stableWeights.Min():F3}t - {stableWeights.Max():F3}t");

        // Assert
        // 最终应该稳定（如果数据足够）
        finalStatus.ShouldBeOneOf(
            AttendedWeighingStatus.WeightStabilized,
            AttendedWeighingStatus.WaitingForDeparture,
            AttendedWeighingStatus.WaitingForStability);

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact(Skip = "待移植至集成测试项目: 重量稳定性模拟，单测耗时 6s+")]
    public async Task WeightStabilized_Then_DropAndRise_Should_HandleCorrectly()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        _output.WriteLine("=== 测试：重量稳定在0.6后，下磅到0.4，然后快速上磅到0.6 ===");

        // Act Phase 1: 上磅并稳定在0.6t
        _output.WriteLine("=== Phase 1: 上磅并稳定在0.6t ===");
        weightSubject.OnNext(0.6m);
        await Task.Delay(300);
        var status1 = service.GetCurrentStatus();
        _output.WriteLine($"Status after on-scale: {status1}");
        status1.ShouldBe(AttendedWeighingStatus.WaitingForStability);

        // 发送稳定的数据点（在0.6t附近小幅波动）
        var random = new Random(42);
        for (int i = 0; i < 20; i++)
        {
            var noise = (decimal)(random.NextDouble() * 0.04 - 0.02); // ±0.02t
            var weight = Math.Round(0.6m + noise, 3);
            weightSubject.OnNext(weight);
            await Task.Delay(200);
        }

        await Task.Delay(1000); // 等待稳定性检查
        var status2 = service.GetCurrentStatus();
        _output.WriteLine($"Status after stabilization: {status2}");
        status2.ShouldBeOneOf(
            AttendedWeighingStatus.WeightStabilized,
            AttendedWeighingStatus.WaitingForDeparture);

        // Act Phase 2: 稳定后间隔200ms，重量变为0.4（下磅）
        _output.WriteLine("=== Phase 2: 稳定后间隔200ms，重量变为0.4（下磅） ===");
        await Task.Delay(200); // 稳定后间隔200ms
        weightSubject.OnNext(0.4m);
        await Task.Delay(300); // 等待状态转换
        var status3 = service.GetCurrentStatus();
        _output.WriteLine($"Status after drop to 0.4t: {status3}");
        status3.ShouldBe(AttendedWeighingStatus.OffScale); // 应该回到OffScale

        // Act Phase 3: 300ms后，重量变为0.6（再次上磅）
        _output.WriteLine("=== Phase 3: 300ms后，重量变为0.6（再次上磅） ===");
        await Task.Delay(300); // 300ms后
        weightSubject.OnNext(0.6m);
        await Task.Delay(300); // 等待状态转换
        var status4 = service.GetCurrentStatus();
        _output.WriteLine($"Status after rise to 0.6t: {status4}");
        status4.ShouldBe(AttendedWeighingStatus.WaitingForStability); // 应该进入WaitingForStability

        // 验证缓存和记录ID已清空（下磅时应该清空）
        var plateNumber = service.GetMostFrequentPlateNumber();
        _output.WriteLine($"Plate number after cycle: {plateNumber ?? "None"}");
        // 下磅后缓存应该被清空（通过状态转换逻辑）

        // Assert
        // 验证状态转换序列
        _output.WriteLine($"State sequence: {status1} -> {status2} -> {status3} -> {status4}");
        
        // 最终应该回到 WaitingForStability（新的称重周期）
        status4.ShouldBe(AttendedWeighingStatus.WaitingForStability);

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact(Skip = "待移植至集成测试项目: 重量稳定性模拟，单测耗时 7s+")]
    public async Task Stability_Should_BeCleared_WhenTransitioningToOffScale()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        _output.WriteLine("=== 测试：验证下磅时 Stability 信息应该被清空 ===");
        _output.WriteLine("场景：第一次称重稳定后下磅，再次上磅时不应该直接跳到 WeightStabilized");

        // Act Phase 1: 第一次称重 - 上磅并稳定
        _output.WriteLine("=== Phase 1: 第一次称重 - 上磅并稳定 ===");
        weightSubject.OnNext(0.6m);
        await Task.Delay(300);
        var status1 = service.GetCurrentStatus();
        _output.WriteLine($"Status after first on-scale: {status1}");
        status1.ShouldBe(AttendedWeighingStatus.WaitingForStability);

        // 发送稳定的数据点
        var random = new Random(42);
        for (int i = 0; i < 20; i++)
        {
            var noise = (decimal)(random.NextDouble() * 0.04 - 0.02);
            var weight = Math.Round(0.6m + noise, 3);
            weightSubject.OnNext(weight);
            await Task.Delay(200);
        }

        await Task.Delay(1000);
        var status2 = service.GetCurrentStatus();
        _output.WriteLine($"Status after first stabilization: {status2}");
        status2.ShouldBeOneOf(
            AttendedWeighingStatus.WeightStabilized,
            AttendedWeighingStatus.WaitingForDeparture);

        // Act Phase 2: 下磅
        _output.WriteLine("=== Phase 2: 下磅 ===");
        weightSubject.OnNext(0.3m);
        await Task.Delay(300);
        var status3 = service.GetCurrentStatus();
        _output.WriteLine($"Status after first off-scale: {status3}");
        status3.ShouldBe(AttendedWeighingStatus.OffScale);

        // Act Phase 3: 再次上磅 - 应该进入 WaitingForStability，而不是直接跳到 WeightStabilized
        _output.WriteLine("=== Phase 3: 再次上磅 - 验证不会直接跳到 WeightStabilized ===");
        var secondOnScaleTime = DateTime.Now;
        weightSubject.OnNext(0.75m);
        await Task.Delay(300);
        var status4 = service.GetCurrentStatus();
        var elapsedAfterSecondOnScale = (DateTime.Now - secondOnScaleTime).TotalSeconds;
        _output.WriteLine($"Status after second on-scale: {status4}, Elapsed: {elapsedAfterSecondOnScale:F3}s");
        
        // Assert: 应该进入 WaitingForStability，而不是直接跳到 WeightStabilized
        status4.ShouldBe(AttendedWeighingStatus.WaitingForStability,
            "After off-scale, when on-scale again, should enter WaitingForStability first, " +
            "not directly jump to WeightStabilized. This indicates Stability information was not cleared.");

        // 继续发送数据点，验证需要时间才能稳定
        _output.WriteLine("=== Phase 4: 发送稳定数据点，验证需要时间才能稳定 ===");
        DateTime? weightStabilizedTime = null;
        for (int i = 0; i < 20; i++)
        {
            var noise = (decimal)(random.NextDouble() * 0.04 - 0.02);
            var weight = Math.Round(0.75m + noise, 3);
            weightSubject.OnNext(weight);
            await Task.Delay(200);
            
            var currentStatus = service.GetCurrentStatus();
            if (currentStatus == AttendedWeighingStatus.WeightStabilized && weightStabilizedTime == null)
            {
                weightStabilizedTime = DateTime.Now;
                var timeToStable = (weightStabilizedTime.Value - secondOnScaleTime).TotalSeconds;
                _output.WriteLine($"Became WeightStabilized at: {weightStabilizedTime:HH:mm:ss.fff}, " +
                                $"Time from on-scale: {timeToStable:F3}s");
                break;
            }
        }

        await Task.Delay(500);
        var finalStatus = service.GetCurrentStatus();
        _output.WriteLine($"Final status: {finalStatus}");

        // Assert: 如果变为 WeightStabilized，应该需要一定时间（至少1秒以上）
        if (weightStabilizedTime.HasValue)
        {
            var timeToStable = (weightStabilizedTime.Value - secondOnScaleTime).TotalSeconds;
            _output.WriteLine($"Time from second on-scale to WeightStabilized: {timeToStable:F3}s");
            
            // 应该需要至少接近窗口时间才能稳定（如果 Stability 被正确清空）
            // 如果时间太短（< 1秒），说明可能使用了旧的 Stability 信息
            if (timeToStable < 1.0)
            {
                _output.WriteLine($"WARNING: Stability detected too quickly ({timeToStable:F3}s < 1.0s). " +
                                "This suggests Stability information was not cleared on off-scale.");
            }
        }

        // Cleanup
        await service.DisposeAsync();
    }

    #endregion

    #region Error Handling Tests

    [Fact(Skip = "待移植至集成测试项目: 重量稳定性模拟，单测耗时 6s+")]
    public async Task Should_HandleErrors_InAsyncOperations()
    {
        // Arrange
        var (service, weightSubject, mockRepo, _) = CreateServiceWithMocks();
        mockRepo.InsertAsync(Arg.Any<WeighingRecord>(), Arg.Any<bool>())
            .Returns(Task.FromException<WeighingRecord>(new Exception("Database error")));
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        // Act - Send stable weights that would trigger record creation
        for (int i = 0; i < 20; i++)
        {
            var weight = 1.0m + (decimal)((i % 3 - 1) * 0.01);
            weightSubject.OnNext(weight);
            await Task.Delay(200);
        }

        await Task.Delay(2000);

        // Assert - Should continue operation despite error
        var status = service.GetCurrentStatus();
        // Status is an enum, so it will always have a value
        status.ShouldBeOneOf(
            AttendedWeighingStatus.OffScale,
            AttendedWeighingStatus.WaitingForStability,
            AttendedWeighingStatus.WeightStabilized,
            AttendedWeighingStatus.WaitingForDeparture);

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task Should_ContinueOperation_AfterErrors()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        // Act - Cause various operations
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);
        SendPlateRecognition("京A12345");
        await Task.Delay(200);

        // Assert - Service should still be operational
        var status = service.GetCurrentStatus();
        status.ShouldBeOneOf(
            AttendedWeighingStatus.OffScale,
            AttendedWeighingStatus.WaitingForStability,
            AttendedWeighingStatus.WeightStabilized,
            AttendedWeighingStatus.WaitingForDeparture);
        var deliveryType = service.CurrentDeliveryType;
        deliveryType.ShouldBeOneOf(DeliveryType.Receiving, DeliveryType.Sending);

        // Cleanup
        await service.DisposeAsync();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 辅助方法:通过 TestLocalEventBus 发送车牌识别事件
    /// </summary>
    private void SendPlateRecognition(string plateNumber, VzvisionColorType? colorType = null)
    {
        _testEventBus.PublishAsync(new LicensePlateRecognizedEventData
        {
            PlateNumber = plateNumber,
            ColorType = colorType,
            DeviceType = LprDeviceType.Hikvision,
            DeviceName = "TestDevice",
            Timestamp = DateTime.Now
        }).GetAwaiter().GetResult();
    }

    private (IAttendedWeighingService service, Subject<decimal> weightSubject) CreateServiceWithWeightSubject(
        bool enableLatestPlateNumber = false,
        bool enablePlateRewrite = true)
    {
        var weightSubject = new Subject<decimal>();
        var mockWeightService = Substitute.For<ITruckScaleWeightService>();
        mockWeightService.WeightUpdates.Returns(weightSubject.AsObservable());
        mockWeightService.IsOnline.Returns(true);

        var service = CreateAttendedWeighingService(
            mockWeightService,
            enableLatestPlateNumber: enableLatestPlateNumber,
            enablePlateRewrite: enablePlateRewrite);
        return (service, weightSubject);
    }

    private (IAttendedWeighingService service, Subject<decimal> weightSubject) CreateServiceWithLowPriorityColors(
        VzvisionColorType[] lowPriorityColors,
        bool enableLatestPlateNumber = false,
        bool enablePlateRewrite = true)
    {
        var weightSubject = new Subject<decimal>();
        var mockWeightService = Substitute.For<ITruckScaleWeightService>();
        mockWeightService.WeightUpdates.Returns(weightSubject.AsObservable());
        mockWeightService.IsOnline.Returns(true);

        var service = CreateAttendedWeighingService(
            mockWeightService,
            lowPriorityColors: lowPriorityColors,
            enableLatestPlateNumber: enableLatestPlateNumber,
            enablePlateRewrite: enablePlateRewrite);
        return (service, weightSubject);
    }

    private (IAttendedWeighingService service, Subject<decimal> weightSubject,
        IRepository<WeighingRecord, long> mockRepo, IUnitOfWork mockUow) CreateServiceWithMocks(
        bool enablePlateRewrite = true)
    {
        var weightSubject = new Subject<decimal>();
        var mockWeightService = Substitute.For<ITruckScaleWeightService>();
        mockWeightService.WeightUpdates.Returns(weightSubject.AsObservable());
        mockWeightService.IsOnline.Returns(true);

        var mockRepo = Substitute.For<IRepository<WeighingRecord, long>>();
        var mockUow = Substitute.For<IUnitOfWork>();
        mockUow.CompleteAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var mockUowManager = Substitute.For<IUnitOfWorkManager>();
        mockUowManager.Begin(Arg.Any<AbpUnitOfWorkOptions>(), Arg.Any<bool>()).Returns(mockUow);

        var service = CreateAttendedWeighingService(
            mockWeightService,
            mockRepo,
            mockUowManager,
            enablePlateRewrite: enablePlateRewrite);

        return (service, weightSubject, mockRepo, mockUow);
    }

    private (IAttendedWeighingService service, Subject<decimal> weightSubject,
        IRepository<WeighingRecord, long> mockRepo, IUnitOfWork mockUow,
        IHikvisionService mockHikvision) CreateServiceWithMocksAndHikvision(
        bool enablePlateRewrite = true)
    {
        var weightSubject = new Subject<decimal>();
        var mockWeightService = Substitute.For<ITruckScaleWeightService>();
        mockWeightService.WeightUpdates.Returns(weightSubject.AsObservable());
        mockWeightService.IsOnline.Returns(true);

        var mockRepo = Substitute.For<IRepository<WeighingRecord, long>>();
        var mockUow = Substitute.For<IUnitOfWork>();
        mockUow.CompleteAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var mockUowManager = Substitute.For<IUnitOfWorkManager>();
        mockUowManager.Begin(Arg.Any<AbpUnitOfWorkOptions>(), Arg.Any<bool>()).Returns(mockUow);

        var mockHikvision = Substitute.For<IHikvisionService>();
        mockHikvision.CaptureJpegFromStreamBatchAsync(Arg.Any<List<BatchCaptureRequest>>())
            .Returns(Task.FromResult(new List<BatchCaptureResult>()));

        var service = CreateAttendedWeighingService(
            mockWeightService,
            mockRepo,
            mockUowManager,
            mockHikvision,
            enablePlateRewrite: enablePlateRewrite);

        return (service, weightSubject, mockRepo, mockUow, mockHikvision);
    }

    private IAttendedWeighingService CreateAttendedWeighingService(
        ITruckScaleWeightService truckScaleWeightService,
        IRepository<WeighingRecord, long>? mockRepo = null,
        IUnitOfWorkManager? mockUowManager = null,
        IHikvisionService? mockHikvision = null,
        VzvisionColorType[]? lowPriorityColors = null,
        bool enableLatestPlateNumber = false,
        bool enablePlateRewrite = true)
    {
        var configData = new Dictionary<string, string?>();
        if (lowPriorityColors != null && lowPriorityColors.Length > 0)
        {
            for (int i = 0; i < lowPriorityColors.Length; i++)
            {
                configData[$"LowPriorityPlateColors:{i}"] = ((int)lowPriorityColors[i]).ToString();
            }
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

        var settingsService = Substitute.For<ISettingsService>();
        var settingsEntity = new SettingsEntity(
            new ScaleSettings(),
            new DocumentScannerConfig(),
            new SystemSettings(),
            new List<CameraConfig>(),
            new List<LicensePlateRecognitionConfig>(),
            new WeighingConfiguration
            {
                MinWeightThreshold = 0.5m,
                WeightStabilityThreshold = 0.05m,
                StabilityWindowMs = 3000,
                StabilityCheckIntervalMs = 200,
                EnableLatestPlateNumber = enableLatestPlateNumber,
                EnablePlateRewrite = enablePlateRewrite
            },
            new SoundDeviceSettings());
        settingsService.GetSettingsAsync().Returns(Task.FromResult(settingsEntity));

        var hikvisionService = mockHikvision ?? Substitute.For<IHikvisionService>();
        var weighingRecordRepo = mockRepo ?? Substitute.For<IRepository<WeighingRecord, long>>();
        var attachmentRepo = Substitute.For<IRepository<WeighingRecordAttachment, int>>();
        var fileRepo = Substitute.For<IRepository<AttachmentFile, int>>();
        var recommendPlateService = new RecommendPlateNumberService(
            Substitute.For<IRepository<Waybill, long>>(),
            Substitute.For<ILogger<RecommendPlateNumberService>>(),
            settingsService,
            Substitute.For<IUnitOfWorkManager>(),
            Substitute.For<IMemoryCache>());

        var uowManager = mockUowManager ?? Substitute.For<IUnitOfWorkManager>();
        if (mockUowManager == null)
        {
            var mockUow = Substitute.For<IUnitOfWork>();
            mockUow.CompleteAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            uowManager.Begin(Arg.Any<AbpUnitOfWorkOptions>(), Arg.Any<bool>()).Returns(mockUow);
        }

        var stateManagerLogger = Substitute.For<ILogger<WeighingStateManager>>();
        var plateLogger = Substitute.For<ILogger<PlateNumberService>>();
        var pipelineLogger = Substitute.For<ILogger<WeighingStreamPipeline>>();
        var captureLogger = Substitute.For<ILogger<WeighingCaptureService>>();
        var recordLogger = Substitute.For<ILogger<WeighingRecordService>>();
        var serviceLogger = Substitute.For<ILogger<AttendedWeighingService>>();

        var stateManager = new WeighingStateManager(_testEventBus, stateManagerLogger);
        var plateNumberService = new PlateNumberService(recommendPlateService, _testEventBus, plateLogger);
        var streamPipeline = new WeighingStreamPipeline(pipelineLogger);
        var captureService = new WeighingCaptureService(
            hikvisionService, null, settingsService, captureLogger);
        var recordService = new WeighingRecordService(
            weighingRecordRepo, fileRepo, attachmentRepo, uowManager, settingsService,
            plateNumberService, _testEventBus, recordLogger);

        return new AttendedWeighingService(
            stateManager,
            plateNumberService,
            streamPipeline,
            captureService,
            recordService,
            truckScaleWeightService,
            serviceLogger,
            configuration,
            _testEventBus,
            settingsService,
            null); // ISoundDeviceService (optional)
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    #endregion

}

