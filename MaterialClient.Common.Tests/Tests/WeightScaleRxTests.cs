using System.Reactive.Linq;
using System.Reactive.Subjects;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests;

/// <summary>
/// 重量稳定性测试
/// 测试内容：使用Rx模拟ITruckScaleWeightService每100ms发送数据，验证AttendedWeighingService的_isWeightStable能否正确判断数据稳定性
/// </summary>
public class WeightScaleRxTests
{
    private readonly ITestOutputHelper _output;

    public WeightScaleRxTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// 测试场景1：模拟100ms间隔发送数据，数据稳定（在±0.05m范围内波动），验证IsWeightStable正确识别稳定状态
    /// </summary>
    [Fact]
    public async Task Test_WeightStable_WhenDataIsStable()
    {
        // Arrange: 使用NSubstitute创建mock，无需手动实现接口
        var weightSubject = new Subject<decimal>();
        var mockWeightService = Substitute.For<ITruckScaleWeightService>();
        mockWeightService.WeightUpdates.Returns(weightSubject.AsObservable());
        mockWeightService.IsOnline.Returns(true);

        var attendedService = CreateAttendedWeighingService(mockWeightService);

        // 启动监控
        await attendedService.StartAsync();

        // Act: 模拟硬件每100ms发送稳定数据（在±0.05m范围内波动）
        var stableWeight = 1.0m;
        var random = new Random(42); // 固定种子以便重现
        var dataPoints = new List<decimal>();

        // 生成30个数据点（3秒），在0.96到1.04之间波动（范围<0.1m，符合稳定条件）
        for (int i = 0; i < 30; i++)
        {
            var noise = (decimal)(random.NextDouble() * 0.08 - 0.04); // ±0.04m范围内波动
            var weight = Math.Round(stableWeight + noise, 2); // 保持2位小数精度
            dataPoints.Add(weight);

            weightSubject.OnNext(weight); // 直接通过Subject发送数据
            _output.WriteLine($"[{i * 100}ms] 发送重量: {weight:F2}m");

            await Task.Delay(100); // 100ms间隔
        }

        // 等待稳定性监控处理完成（Buffer需要3秒窗口）
        await Task.Delay(500);

        // Assert: 验证重量被识别为稳定
        var isStable = attendedService.IsWeightStable;
        _output.WriteLine($"\n最终稳定状态: {isStable}");
        _output.WriteLine(
            $"数据范围: {dataPoints.Min():F2}m ~ {dataPoints.Max():F2}m (差值: {(dataPoints.Max() - dataPoints.Min()):F2}m)");

        Assert.True(isStable, "数据在±0.05m范围内波动，应该被识别为稳定");

        // Cleanup
        await attendedService.StopAsync();
        await attendedService.DisposeAsync();
        weightSubject.Dispose();
    }

    /// <summary>
    /// 测试场景2：模拟100ms间隔发送数据，数据持续大幅变化，验证IsWeightStable正确识别不稳定状态
    /// </summary>
    [Fact]
    public async Task Test_WeightUnstable_WhenDataFluctuates()
    {
        // Arrange
        var weightSubject = new Subject<decimal>();
        var mockWeightService = Substitute.For<ITruckScaleWeightService>();
        mockWeightService.WeightUpdates.Returns(weightSubject.AsObservable());
        mockWeightService.IsOnline.Returns(true);

        var attendedService = CreateAttendedWeighingService(mockWeightService);

        await attendedService.StartAsync();

        // Act: 模拟硬件每100ms发送不稳定数据（变化超过0.1m）
        var dataPoints = new List<decimal>();

        // 生成30个数据点（3秒），重量从0.5到1.5大幅变化
        for (int i = 0; i < 30; i++)
        {
            var weight = Math.Round(0.5m + (i * 0.05m), 2); // 每次增加0.05m，保持2位小数
            dataPoints.Add(weight);

            weightSubject.OnNext(weight);
            _output.WriteLine($"[{i * 100}ms] 发送重量: {weight:F2}m");

            await Task.Delay(100);
        }

        // 等待稳定性监控处理
        await Task.Delay(500);

        // Assert: 验证重量被识别为不稳定
        var isStable = attendedService.IsWeightStable;
        _output.WriteLine($"\n最终稳定状态: {isStable}");
        _output.WriteLine(
            $"数据范围: {dataPoints.Min():F2}m ~ {dataPoints.Max():F2}m (差值: {(dataPoints.Max() - dataPoints.Min()):F2}m)");

        Assert.False(isStable, "数据变化超过0.1m，应该被识别为不稳定");

        // Cleanup
        await attendedService.StopAsync();
        await attendedService.DisposeAsync();
        weightSubject.Dispose();
    }

    /// <summary>
    /// 测试场景3：从不稳定到稳定的过渡
    /// </summary>
    [Fact]
    public async Task Test_WeightTransition_FromUnstableToStable()
    {
        // Arrange
        var weightSubject = new Subject<decimal>();
        var mockWeightService = Substitute.For<ITruckScaleWeightService>();
        mockWeightService.WeightUpdates.Returns(weightSubject.AsObservable());
        mockWeightService.IsOnline.Returns(true);

        var attendedService = CreateAttendedWeighingService(mockWeightService);

        await attendedService.StartAsync();

        // Act Phase 1: 先发送不稳定数据（2秒）
        _output.WriteLine("=== 阶段1: 发送不稳定数据 ===");
        for (int i = 0; i < 20; i++)
        {
            var weight = Math.Round(0.5m + (i * 0.1m), 2); // 保持2位小数
            weightSubject.OnNext(weight);
            _output.WriteLine($"[{i * 100}ms] 不稳定数据: {weight:F2}m");
            await Task.Delay(100);
        }

        await Task.Delay(500);
        var isStablePhase1 = attendedService.IsWeightStable;
        _output.WriteLine($"阶段1稳定状态: {isStablePhase1}\n");

        // Act Phase 2: 发送稳定数据（3秒）
        _output.WriteLine("=== 阶段2: 发送稳定数据 ===");
        var stableWeight = 2.0m;
        var random = new Random(42);
        for (int i = 0; i < 30; i++)
        {
            var noise = (decimal)(random.NextDouble() * 0.08 - 0.04);
            var weight = Math.Round(stableWeight + noise, 2); // 保持2位小数
            weightSubject.OnNext(weight);
            _output.WriteLine($"[{i * 100}ms] 稳定数据: {weight:F2}m");
            await Task.Delay(100);
        }

        await Task.Delay(500);
        var isStablePhase2 = attendedService.IsWeightStable;
        _output.WriteLine($"阶段2稳定状态: {isStablePhase2}");

        // Assert
        Assert.False(isStablePhase1, "阶段1应该是不稳定");
        Assert.True(isStablePhase2, "阶段2应该是稳定");

        // Cleanup
        await attendedService.StopAsync();
        await attendedService.DisposeAsync();
        weightSubject.Dispose();
    }

    /// <summary>
    /// 创建AttendedWeighingService实例，使用NSubstitute Mock所有依赖项
    /// </summary>
    private AttendedWeighingService CreateAttendedWeighingService(
        ITruckScaleWeightService truckScaleWeightService)
    {
        // Mock所有依赖项 - 无需关心具体实现
        var settingsService = Substitute.For<ISettingsService>();
        var settingsEntity = new SettingsEntity(
            new ScaleSettings(),
            new DocumentScannerConfig(),
            new SystemSettings(),
            new List<CameraConfig>(),
            new List<LicensePlateRecognitionConfig>()
        );
        settingsService.GetSettingsAsync().Returns(Task.FromResult(settingsEntity));

        var hikvisionService = Substitute.For<IHikvisionService>();
        var weighingRecordRepo = Substitute.For<IRepository<WeighingRecord, long>>();
        var attachmentRepo = Substitute.For<IRepository<WeighingRecordAttachment, int>>();
        var fileRepo = Substitute.For<IRepository<AttachmentFile, int>>();

        var mockUow = Substitute.For<IUnitOfWork>();
        mockUow.CompleteAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var uowManager = Substitute.For<IUnitOfWorkManager>();
        uowManager.Begin(Arg.Any<AbpUnitOfWorkOptions>(), Arg.Any<bool>()).Returns(mockUow);

        var logger = Substitute.For<ILogger<AttendedWeighingService>>();

        var eventBus = Substitute.For<Volo.Abp.EventBus.Local.ILocalEventBus>();

        // 创建服务实例
        return new AttendedWeighingService(
            fileRepo,
            hikvisionService,
            eventBus,
            logger,
            settingsService,
            truckScaleWeightService,
            uowManager,
            attachmentRepo,
            weighingRecordRepo
        );
    }
}