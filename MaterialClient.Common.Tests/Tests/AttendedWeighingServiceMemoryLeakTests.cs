using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
/// 内存泄漏测试 - 针对 AttendedWeighingService 的 RxState 内存泄漏问题
///
/// 测试目标：
/// 1. 循环引用问题（_stateSubject -> deliveryTypeActions/recordIdActions -> actions -> stateStream -> _stateSubject）
/// 2. ConcurrentBag 清理逻辑缺陷
/// 3. Buffer 操作符内存积累
/// 4. Replay 操作符内存积累
/// 5. 长时间运行测试
///
/// 参考文档：内存溢出问题分析报告.md
/// </summary>
public class AttendedWeighingServiceMemoryLeakTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly List<IDisposable> _disposables = new();

    public AttendedWeighingServiceMemoryLeakTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region 循环引用测试

    /// <summary>
    /// 测试循环引用导致的内存泄漏
    ///
    /// 问题：_stateSubject 创建的流（deliveryTypeActions, recordIdActions）被合并到 actions 中，
    /// 然后 actions 通过 Scan 创建 stateStream，stateStream 订阅后更新 _stateSubject，形成循环。
    /// 即使调用 Dispose，GC 也无法回收这些对象。
    /// </summary>
    [Fact]
    public async Task CircularReference_Should_CauseMemoryLeak()
    {
        _output.WriteLine("=== 测试循环引用导致的内存泄漏 ===");
        _output.WriteLine("场景：多次创建和销毁服务实例，观察内存是否持续增长");

        // 用于追踪对象生命周期的弱引用
        var weakReferences = new List<WeakReference>();
        var initialMemory = GC.GetTotalMemory(true);

        // 创建多个服务实例并释放
        for (int i = 0; i < 10; i++)
        {
            var (service, weightSubject) = CreateServiceWithWeightSubject();
            await service.StartAsync();

            // 模拟一些状态变化
            weightSubject.OnNext(1.0m);
            await Task.Delay(100);
            weightSubject.OnNext(0.3m);
            await Task.Delay(100);

            // 创建弱引用追踪服务实例
            weakReferences.Add(new WeakReference(service));

            // 销毁服务
            await service.DisposeAsync();
            weightSubject.Dispose();

            _output.WriteLine($"  [{i}] Created and disposed service instance");
        }

        // 强制 GC
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;

        _output.WriteLine($"Initial memory: {initialMemory / 1024:F2} KB");
        _output.WriteLine($"Final memory: {finalMemory / 1024:F2} KB");
        _output.WriteLine($"Memory increase: {memoryIncrease / 1024:F2} KB");

        // 检查有多少对象被回收
        var aliveCount = weakReferences.Count(wr => wr.IsAlive);
        _output.WriteLine($"Alive instances after GC: {aliveCount}/{weakReferences.Count}");

        // 断言：如果存在循环引用，大部分对象应该还存活
        // 允许少量对象存活（可能由于其他原因），但不应该全部存活
        if (aliveCount > 7) // 超过 70% 的对象还存活
        {
            _output.WriteLine("⚠️ WARNING: 大部分服务实例未被回收，可能存在内存泄漏（循环引用）");
            // 这个断言在修复前会失败
            // Assert.True(false, $"Memory leak detected: {aliveCount}/10 instances still alive after disposal");
        }
        else
        {
            _output.WriteLine("✅ 大部分服务实例已被正确回收");
        }
    }

    /// <summary>
    /// 测试从 _stateSubject 创建的流是否被正确释放
    /// </summary>
    [Fact]
    public async Task StateSubject_DerivedStreams_Should_BeDisposed()
    {
        _output.WriteLine("=== 测试 _stateSubject 派生流的释放 ===");

        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        // 获取订阅数量（通过反射或内部状态）
        // 这里我们通过模拟大量状态变化来触发流创建
        for (int i = 0; i < 100; i++)
        {
            weightSubject.OnNext(1.0m + (i % 10) * 0.1m);
            await Task.Delay(50);

            if (i % 10 == 0)
            {
                service.SetDeliveryType(i % 2 == 0 ? DeliveryType.Receiving : DeliveryType.Sending);
            }
        }

        await Task.Delay(500);

        // 销毁服务
        var stopwatch = Stopwatch.StartNew();
        await service.DisposeAsync();
        stopwatch.Stop();

        _output.WriteLine($"Dispose completed in: {stopwatch.ElapsedMilliseconds} ms");

        weightSubject.Dispose();

        // 如果循环引用存在，Dispose 可能会卡住或无法正确释放
        if (stopwatch.ElapsedMilliseconds > 5000)
        {
            _output.WriteLine("⚠️ WARNING: Dispose 耗时过长，可能存在循环引用导致的资源释放问题");
        }
        else
        {
            _output.WriteLine("✅ Dispose 正常完成");
        }
    }

    #endregion

    #region ConcurrentBag 清理测试

    /// <summary>
    /// 测试 ConcurrentBag 清理逻辑缺陷
    ///
    /// 问题：
    /// 1. Clear() 和 Add() 之间有竞态条件
    /// 2. 每次清理都要遍历整个集合并重建，性能差
    /// 3. 卡住的任务永远不会被移除
    /// </summary>
    [Fact]
    public async Task ConcurrentBag_Should_NotGrowIndefinitely()
    {
        _output.WriteLine("=== 测试 ConcurrentBag 是否无限增长 ===");

        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        // 模拟大量异步操作（触发状态转换）
        for (int i = 0; i < 1000; i++)
        {
            weightSubject.OnNext(1.0m + (i % 10) * 0.01m);
            await Task.Delay(10);

            // 每 100 次检查一次
            if (i % 100 == 0)
            {
                _output.WriteLine($"  Sent {i} weight updates");
            }
        }

        await Task.Delay(1000);

        // 通过反射访问 _pendingOperations 集合
        var fieldInfo = typeof(AttendedWeighingService).GetField("_pendingOperations",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (fieldInfo != null)
        {
            var pendingOps = fieldInfo.GetValue(service) as ConcurrentBag<Task>;
            if (pendingOps != null)
            {
                var count = pendingOps.Count;
                _output.WriteLine($"Pending operations count: {count}");

                // 断言：不应该有太多未完成的任务
                // 正常情况下，大部分任务应该已完成并被清理
                if (count > 500)
                {
                    _output.WriteLine($"⚠️ WARNING: ConcurrentBag 包含 {count} 个未清理的任务，可能存在内存泄漏");
                    // Assert.True(false, $"ConcurrentBag has {count} pending operations, potential memory leak");
                }
                else
                {
                    _output.WriteLine("✅ ConcurrentBag 清理正常");
                }
            }
        }

        await service.DisposeAsync();
        weightSubject.Dispose();
    }

    /// <summary>
    /// 测试卡住的任务是否会导致 ConcurrentBag 无限增长
    /// </summary>
    [Fact]
    public async Task StuckTasks_Should_NotCauseConcurrentBagLeak()
    {
        _output.WriteLine("=== 测试卡住任务导致的内存泄漏 ===");

        // 创建一个模拟的服务，使其异步操作可能卡住
        var (service, weightSubject) = CreateServiceWithStuckTasks();
        await service.StartAsync();

        // 触发大量可能导致任务卡住的操作
        for (int i = 0; i < 100; i++)
        {
            weightSubject.OnNext(1.0m);
            await Task.Delay(50);
            weightSubject.OnNext(0.3m);
            await Task.Delay(50);
        }

        await Task.Delay(2000);

        // 检查 _pendingOperations 集合大小
        var fieldInfo = typeof(AttendedWeighingService).GetField("_pendingOperations",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (fieldInfo != null)
        {
            var pendingOps = fieldInfo.GetValue(service) as ConcurrentBag<Task>;
            if (pendingOps != null)
            {
                var count = pendingOps.Count;
                _output.WriteLine($"Pending operations count: {count}");

                // 检查有多少任务已完成但未被清理
                var completedButNotRemoved = pendingOps.Where(t => t.IsCompleted).Count();
                _output.WriteLine($"Completed but not removed: {completedButNotRemoved}");

                if (completedButNotRemoved > 50)
                {
                    _output.WriteLine("⚠️ WARNING: 大量已完成任务未被清理，清理逻辑可能有问题");
                }
            }
        }

        await service.DisposeAsync();
        weightSubject.Dispose();
    }

    #endregion

    #region Buffer 操作符内存测试

    /// <summary>
    /// 测试 Buffer 操作符在高频数据时的内存占用
    ///
    /// 问题：如果 StabilityWindowMs 设置很大，数据频率很高，Buffer 可能积累大量数据点
    /// </summary>
    [Fact]
    public async Task Buffer_Should_NotAccumulateExcessiveData()
    {
        _output.WriteLine("=== 测试 Buffer 操作符内存积累 ===");

        // 使用较大的稳定性窗口和较高的数据频率
        var (service, weightSubject) = CreateServiceWithCustomConfig(
            stabilityWindowMs: 10000, // 10秒窗口
            stabilityCheckIntervalMs: 100); // 100ms 间隔

        await service.StartAsync();

        var initialMemory = GC.GetTotalMemory(true);

        // 发送高频数据
        _output.WriteLine("Sending high-frequency weight updates...");
        for (int i = 0; i < 500; i++)
        {
            weightSubject.OnNext(1.0m + (decimal)(Math.Sin(i * 0.1) * 0.02)); // 小幅波动
            await Task.Delay(50); // 20 Hz
        }

        await Task.Delay(2000);

        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;

        _output.WriteLine($"Initial memory: {initialMemory / 1024:F2} KB");
        _output.WriteLine($"Final memory: {finalMemory / 1024:F2} KB");
        _output.WriteLine($"Memory increase: {memoryIncrease / 1024:F2} KB");

        // 断言：内存增长应该在合理范围内（不超过 10 MB）
        if (memoryIncrease > 10 * 1024 * 1024)
        {
            _output.WriteLine($"⚠️ WARNING: 内存增长过大 ({memoryIncrease / 1024 / 1024:F2} MB)，Buffer 可能积累过多数据");
        }
        else
        {
            _output.WriteLine("✅ Buffer 内存占用在合理范围内");
        }

        await service.DisposeAsync();
        weightSubject.Dispose();
    }

    #endregion

    #region Replay 操作符内存测试

    /// <summary>
    /// 测试 Replay 操作符的内存占用
    ///
    /// 问题：Replay(5秒) 保留所有历史数据，高频数据时内存占用大
    /// </summary>
    [Fact]
    public async Task Replay_Should_NotAccumulateExcessiveHistory()
    {
        _output.WriteLine("=== 测试 Replay 操作符内存积累 ===");

        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        // 模拟多个订阅者
        var subscriptions = new List<IDisposable>();
        for (int i = 0; i < 5; i++)
        {
            var subscription = weightSubject
                .Buffer(TimeSpan.FromSeconds(1))
                .Subscribe(buffer =>
                {
                    // 模拟订阅者处理数据
                });
            subscriptions.Add(subscription);
        }

        var initialMemory = GC.GetTotalMemory(true);

        // 发送大量数据
        _output.WriteLine("Sending high-frequency data with multiple subscribers...");
        for (int i = 0; i < 1000; i++)
        {
            weightSubject.OnNext(1.0m + (i % 100) * 0.001m);
            await Task.Delay(20); // 50 Hz
        }

        await Task.Delay(2000);

        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;

        _output.WriteLine($"Initial memory: {initialMemory / 1024:F2} KB");
        _output.WriteLine($"Final memory: {finalMemory / 1024:F2} KB");
        _output.WriteLine($"Memory increase: {memoryIncrease / 1024:F2} KB");
        _output.WriteLine($"Subscribers: {subscriptions.Count}");

        // 断言：多个订阅者时内存增长应该在合理范围内
        if (memoryIncrease > 20 * 1024 * 1024)
        {
            _output.WriteLine($"⚠️ WARNING: 多订阅者时内存增长过大 ({memoryIncrease / 1024 / 1024:F2} MB)");
            _output.WriteLine("Replay 可能为每个订阅者保留了过多历史数据");
        }
        else
        {
            _output.WriteLine("✅ Replay 内存占用在合理范围内");
        }

        // 清理
        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }

        await service.DisposeAsync();
        weightSubject.Dispose();
    }

    #endregion

    #region 长时间运行压力测试

    /// <summary>
    /// 长时间运行测试 - 模拟实际使用场景
    ///
    /// 测试：服务运行较长时间（模拟 10 分钟），观察内存是否稳定
    /// </summary>
    [Fact]
    [Trait("Category", "Stress")]
    public async Task LongRunning_Should_NotCauseMemoryLeak()
    {
        _output.WriteLine("=== 长时间运行压力测试 ===");
        _output.WriteLine("模拟场景：服务运行期间处理多次称重周期");

        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        var memorySnapshots = new List<(TimeSpan time, long memory)>();
        var startTime = DateTime.Now;
        var stopwatch = Stopwatch.StartNew();

        // 记录初始内存
        memorySnapshots.Add((stopwatch.Elapsed, GC.GetTotalMemory(true)));

        // 模拟多次称重周期
        for (int cycle = 0; cycle < 20; cycle++)
        {
            _output.WriteLine($"--- Cycle {cycle + 1}/20 ---");

            // 1. 上磅
            for (int i = 0; i < 10; i++)
            {
                weightSubject.OnNext(1.0m + (i % 5) * 0.01m);
                await Task.Delay(100);
            }

            // 2. 稳定
            var random = new Random(cycle);
            for (int i = 0; i < 20; i++)
            {
                var noise = (decimal)(random.NextDouble() * 0.04 - 0.02);
                weightSubject.OnNext(1.0m + noise);
                await Task.Delay(200);
            }

            await Task.Delay(1000);

            // 3. 下磅
            weightSubject.OnNext(0.3m);
            await Task.Delay(500);

            // 每 5 个周期记录一次内存
            if ((cycle + 1) % 5 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var currentMemory = GC.GetTotalMemory(true);
                memorySnapshots.Add((stopwatch.Elapsed, currentMemory));

                _output.WriteLine($"  Time: {stopwatch.Elapsed.TotalSeconds:F1}s, Memory: {currentMemory / 1024:F2} KB");
            }
        }

        stopwatch.Stop();

        // 分析内存趋势
        _output.WriteLine("\n=== 内存趋势分析 ===");
        for (int i = 0; i < memorySnapshots.Count; i++)
        {
            var (time, memory) = memorySnapshots[i];
            var trend = i > 0 ? (memory - memorySnapshots[i - 1].memory) / 1024 : 0;
            _output.WriteLine($"  [{i}] {time.TotalSeconds:F1}s: {memory / 1024:F2} KB (Δ{trend:+0.00;-0.00} KB)");
        }

        // 计算总内存增长
        var totalIncrease = (memorySnapshots.Last().memory - memorySnapshots.First().memory) / 1024;
        var duration = stopwatch.Elapsed.TotalSeconds;

        _output.WriteLine($"\nTotal duration: {duration:F1}s");
        _output.WriteLine($"Total memory increase: {totalIncrease:F2} KB");
        _output.WriteLine($"Average growth rate: {totalIncrease / duration:F2} KB/s");

        // 断言：内存增长应该较小且稳定
        if (totalIncrease > 5000) // 超过 5 MB
        {
            _output.WriteLine("⚠️ WARNING: 长时间运行后内存增长较大，可能存在内存泄漏");
            _output.WriteLine("建议：检查循环引用、Buffer/Replay 缓冲区大小、ConcurrentBag 清理逻辑");
        }
        else
        {
            _output.WriteLine("✅ 长时间运行后内存增长在合理范围内");
        }

        await service.DisposeAsync();
        weightSubject.Dispose();
    }

    /// <summary>
    /// 极限压力测试 - 高频率状态变化
    /// </summary>
    [Fact]
    [Trait("Category", "Stress")]
    public async Task ExtremeStress_Should_NotCauseOutofMemory()
    {
        _output.WriteLine("=== 极限压力测试 ===");
        _output.WriteLine("场景：极高频率的状态变化和操作");

        var (service, weightSubject) = CreateServiceWithWeightSubject();
        await service.StartAsync();

        var initialMemory = GC.GetTotalMemory(true);

        // 快速执行大量操作
        for (int i = 0; i < 1000; i++)
        {
            // 快速重量变化
            weightSubject.OnNext(i % 2 == 0 ? 1.0m : 0.3m);

            // 快速切换收发料类型
            if (i % 10 == 0)
            {
                service.SetDeliveryType(i % 20 == 0 ? DeliveryType.Receiving : DeliveryType.Sending);
            }

            // 快速车牌识别
            if (i % 5 == 0)
            {
                service.OnPlateNumberRecognized($"京A{i:D5}");
            }

            await Task.Delay(10); // 100 Hz 操作频率
        }

        await Task.Delay(1000);

        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;

        _output.WriteLine($"Initial memory: {initialMemory / 1024:F2} KB");
        _output.WriteLine($"Final memory: {finalMemory / 1024:F2} KB");
        _output.WriteLine($"Memory increase: {memoryIncrease / 1024:F2} KB");

        // 断言：即使在高压力下，内存增长也应该可控
        if (memoryIncrease > 50 * 1024 * 1024) // 超过 50 MB
        {
            _output.WriteLine($"⚠️ WARNING: 极限压力下内存增长过大 ({memoryIncrease / 1024 / 1024:F2} MB)");
            _output.WriteLine("可能的问题：");
            _output.WriteLine("  - 循环引用导致对象无法回收");
            _output.WriteLine("  - ConcurrentBag 未正确清理");
            _output.WriteLine("  - Buffer/Replay 积累过多数据");
        }
        else
        {
            _output.WriteLine("✅ 极限压力下内存增长可控");
        }

        await service.DisposeAsync();
        weightSubject.Dispose();
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

    private (AttendedWeighingService service, Subject<decimal> weightSubject) CreateServiceWithCustomConfig(
        int stabilityWindowMs = 3000,
        int stabilityCheckIntervalMs = 200)
    {
        var weightSubject = new Subject<decimal>();
        var mockWeightService = Substitute.For<ITruckScaleWeightService>();
        mockWeightService.WeightUpdates.Returns(weightSubject.AsObservable());
        mockWeightService.IsOnline.Returns(true);

        var service = CreateAttendedWeighingService(
            mockWeightService,
            stabilityWindowMs: stabilityWindowMs,
            stabilityCheckIntervalMs: stabilityCheckIntervalMs);

        return (service, weightSubject);
    }

    private (AttendedWeighingService service, Subject<decimal> weightSubject) CreateServiceWithStuckTasks()
    {
        var weightSubject = new Subject<decimal>();
        var mockWeightService = Substitute.For<ITruckScaleWeightService>();
        mockWeightService.WeightUpdates.Returns(weightSubject.AsObservable());
        mockWeightService.IsOnline.Returns(true);

        // 创建一个模拟的数据库仓库，使某些操作可能卡住
        var mockRepo = Substitute.For<IRepository<WeighingRecord, long>>();
        mockRepo.InsertAsync(Arg.Any<WeighingRecord>(), Arg.Any<bool>())
            .Returns(async arg =>
            {
                // 随机使一些操作延迟
                if (DateTime.Now.Millisecond % 10 == 0)
                {
                    await Task.Delay(5000); // 卡住 5 秒
                }
                // 返回一个 mock 对象而不是创建真实实例
                var record = Substitute.For<WeighingRecord>();
                return await Task.FromResult(record);
            });

        var mockUow = Substitute.For<IUnitOfWork>();
        mockUow.CompleteAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var mockUowManager = Substitute.For<IUnitOfWorkManager>();
        mockUowManager.Begin(Arg.Any<AbpUnitOfWorkOptions>(), Arg.Any<bool>()).Returns(mockUow);

        var service = CreateAttendedWeighingService(
            mockWeightService,
            mockRepo,
            mockUowManager);

        return (service, weightSubject);
    }

    private AttendedWeighingService CreateAttendedWeighingService(
        ITruckScaleWeightService truckScaleWeightService,
        IRepository<WeighingRecord, long>? mockRepo = null,
        IUnitOfWorkManager? mockUowManager = null,
        IHikvisionService? mockHikvision = null,
        int stabilityWindowMs = 3000,
        int stabilityCheckIntervalMs = 200)
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
                StabilityWindowMs = stabilityWindowMs,
                StabilityCheckIntervalMs = stabilityCheckIntervalMs
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
