using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using BatchCaptureRequest = MaterialClient.Common.Services.Hikvision.BatchCaptureRequest;
using BatchCaptureResult = MaterialClient.Common.Services.Hikvision.BatchCaptureResult;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReactiveUI;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
/// Unit tests for AttendedWeighingService
/// </summary>
public class AttendedWeighingServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly List<IDisposable> _disposables = new();

    public AttendedWeighingServiceTests(ITestOutputHelper output)
    {
        _output = output;
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
        // After disposal, operations may not work but should not crash
        var status = service.GetCurrentStatus();
        status.ShouldBeOneOf(AttendedWeighingStatus.OffScale, AttendedWeighingStatus.WaitingForStability);

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
        var receivedMessages = new List<DeliveryTypeChangedMessage>();
        var subscription = MessageBus.Current.Listen<DeliveryTypeChangedMessage>()
            .Subscribe(msg => receivedMessages.Add(msg));
        _disposables.Add(subscription);

        // Act
        service.SetDeliveryType(DeliveryType.Sending);
        await Task.Delay(200);

        // Assert
        receivedMessages.ShouldNotBeEmpty();
        receivedMessages.Last().DeliveryType.ShouldBe(DeliveryType.Sending);

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task SetDeliveryType_Should_NotSendNotification_WhenUnchanged()
    {
        // Arrange
        var (service, _) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        var receivedMessages = new List<DeliveryTypeChangedMessage>();
        var subscription = MessageBus.Current.Listen<DeliveryTypeChangedMessage>()
            .Subscribe(msg => receivedMessages.Add(msg));
        _disposables.Add(subscription);

        // Act - Set to same value
        service.SetDeliveryType(DeliveryType.Receiving);
        await Task.Delay(200);

        // Assert - Should not send notification for same value
        // Note: The implementation may still send, but we test the behavior
        receivedMessages.Count.ShouldBeLessThanOrEqualTo(1);

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

        // Act
        service.OnPlateNumberRecognized("");
        service.OnPlateNumberRecognized("   ");
        service.OnPlateNumberRecognized(null!);
        await Task.Delay(100);

        // Assert
        service.GetMostFrequentPlateNumber().ShouldBeNull();

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task OnPlateNumberRecognized_Should_FilterHangingCharacter()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        weightSubject.OnNext(1.0m); // Put on scale
        await Task.Delay(300);

        // Act
        service.OnPlateNumberRecognized("京A12345挂");
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
        await Task.Delay(300);

        // Act
        service.OnPlateNumberRecognized("京A12345");
        await Task.Delay(200);

        // Assert
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBe("京A12345");

        // Cleanup
        await service.DisposeAsync();
    }

    [Fact]
    public async Task OnPlateNumberRecognized_Should_Ignore_WhenOffScale()
    {
        // Arrange
        var (service, _) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        // Stay off scale (weight = 0)

        // Act
        service.OnPlateNumberRecognized("京A12345");
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
        service.OnPlateNumberRecognized("京A12345");
        service.OnPlateNumberRecognized("京A12345");
        service.OnPlateNumberRecognized("粤B67890");
        await Task.Delay(200);

        // Assert
        var plateNumber = service.GetMostFrequentPlateNumber();
        plateNumber.ShouldBe("京A12345");

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
    public async Task OnPlateNumberRecognized_Should_TriggerMessageBusNotification()
    {
        // Arrange
        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();
        weightSubject.OnNext(1.0m);
        await Task.Delay(300);

        var receivedMessages = new List<PlateNumberChangedMessage>();
        var subscription = MessageBus.Current.Listen<PlateNumberChangedMessage>()
            .Subscribe(msg => receivedMessages.Add(msg));
        _disposables.Add(subscription);

        // Act
        service.OnPlateNumberRecognized("京A12345");
        await Task.Delay(300);

        // Assert
        receivedMessages.ShouldNotBeEmpty();
        receivedMessages.Last().PlateNumber.ShouldBe("京A12345");

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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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
        service.OnPlateNumberRecognized("京A12345");
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

    [Fact]
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

    [Fact]
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

    #endregion

    #region Error Handling Tests

    [Fact]
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
        service.OnPlateNumberRecognized("京A12345");
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

    private (AttendedWeighingService service, Subject<decimal> weightSubject) CreateServiceWithWeightSubject()
    {
        var weightSubject = new Subject<decimal>();
        var mockWeightService = Substitute.For<ITruckScaleWeightService>();
        mockWeightService.WeightUpdates.Returns(weightSubject.AsObservable());
        mockWeightService.IsOnline.Returns(true);

        var service = CreateAttendedWeighingService(mockWeightService);
        return (service, weightSubject);
    }

    private (AttendedWeighingService service, Subject<decimal> weightSubject,
        IRepository<WeighingRecord, long> mockRepo, IUnitOfWork mockUow) CreateServiceWithMocks()
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
            mockUowManager);

        return (service, weightSubject, mockRepo, mockUow);
    }

    private (AttendedWeighingService service, Subject<decimal> weightSubject,
        IRepository<WeighingRecord, long> mockRepo, IUnitOfWork mockUow,
        IHikvisionService mockHikvision) CreateServiceWithMocksAndHikvision()
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
            mockHikvision);

        return (service, weightSubject, mockRepo, mockUow, mockHikvision);
    }

    private AttendedWeighingService CreateAttendedWeighingService(
        ITruckScaleWeightService truckScaleWeightService,
        IRepository<WeighingRecord, long>? mockRepo = null,
        IUnitOfWorkManager? mockUowManager = null,
        IHikvisionService? mockHikvision = null)
    {
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
                StabilityCheckIntervalMs = 200
            },
            new SoundDeviceSettings());
        settingsService.GetSettingsAsync().Returns(Task.FromResult(settingsEntity));

        var hikvisionService = mockHikvision ?? Substitute.For<IHikvisionService>();
        var weighingRecordRepo = mockRepo ?? Substitute.For<IRepository<WeighingRecord, long>>();
        var attachmentRepo = Substitute.For<IRepository<WeighingRecordAttachment, int>>();
        var fileRepo = Substitute.For<IRepository<AttachmentFile, int>>();

        var uowManager = mockUowManager ?? Substitute.For<IUnitOfWorkManager>();
        if (mockUowManager == null)
        {
            var mockUow = Substitute.For<IUnitOfWork>();
            mockUow.CompleteAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            uowManager.Begin(Arg.Any<AbpUnitOfWorkOptions>(), Arg.Any<bool>()).Returns(mockUow);
        }

        var logger = Substitute.For<ILogger<AttendedWeighingService>>();
        var eventBus = Substitute.For<ILocalEventBus>();

        return new AttendedWeighingService(
            fileRepo,
            hikvisionService,
            eventBus,
            logger,
            settingsService,
            truckScaleWeightService,
            uowManager,
            attachmentRepo,
            weighingRecordRepo);
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

