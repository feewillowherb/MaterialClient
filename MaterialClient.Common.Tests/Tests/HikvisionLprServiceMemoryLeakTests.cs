using System.Diagnostics;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Tests.Mocks;
using ReactiveUI;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     海康威视车牌识别服务内存泄漏测试
/// </summary>
public class HikvisionLprServiceMemoryLeakTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public HikvisionLprServiceMemoryLeakTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task StartStopRepeatedly_ShouldNotLeakMemory()
    {
        // Arrange
        var service = new MockHikvisionLprService();
        var iterations = 100;

        // 强制 GC 并记录初始内存
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);

        _output.WriteLine($"初始内存: {initialMemory / 1024} KB");

        // Act
        for (var i = 0; i < iterations; i++)
        {
            await service.StartAsync();
            await service.StopAsync();

            if (i % 10 == 0)
            {
                _output.WriteLine($"已完成 {i}/{iterations} 次启动/停止");
            }
        }

        // 强制 GC 并记录最终内存
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryDelta = finalMemory - initialMemory;

        _output.WriteLine($"最终内存: {finalMemory / 1024} KB");
        _output.WriteLine($"内存变化: {memoryDelta / 1024} KB");
        _output.WriteLine($"每次启动/停止平均内存变化: {memoryDelta / iterations} bytes");

        // Assert: 内存增长应该小于 1MB
        // 注意: 这里使用较大的阈值，因为 JIT 编译、缓存等因素可能影响内存使用
        Assert.True(memoryDelta < 1024 * 1024,
            $"内存泄漏检测: 在 {iterations} 次启动/停止后，内存增长了 {memoryDelta / 1024} KB");
    }

    [Fact]
    public async Task ManyDeviceEvents_ShouldNotLeakMemory()
    {
        // Arrange
        var service = new MockHikvisionLprService();
        var eventCount = 10000;

        // 订阅 MessageBus（模拟真实场景）
        var subscription = MessageBus.Current.Listen<LicensePlateRecognizedMessage>().Subscribe(msg =>
        {
            var _ = msg.PlateNumber;
        });

        // 强制 GC 并记录初始内存
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);

        _output.WriteLine($"初始内存: {initialMemory / 1024} KB");

        // Act: 生成大量事件
        for (var i = 0; i < eventCount; i++)
        {
            service.SimulatePlateRecognition(
                $"京A{i:D5}",
                $"Camera{i % 10}",
                i % 2 == 0 ? LicensePlateDirection.In : LicensePlateDirection.Out);

            if (i % 1000 == 0)
            {
                _output.WriteLine($"已生成 {i}/{eventCount} 个事件");
            }
        }

        // 强制 GC 并记录最终内存
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryDelta = finalMemory - initialMemory;

        _output.WriteLine($"最终内存: {finalMemory / 1024} KB");
        _output.WriteLine($"内存变化: {memoryDelta / 1024} KB");
        _output.WriteLine($"每个事件平均内存变化: {memoryDelta / eventCount} bytes");

        // 清理
        subscription.Dispose();

        // Assert: 内存增长应该合理
        // 注意: Mock 服务会保留所有事件，所以内存增长是预期的
        // 我们主要检查是否有异常的内存泄漏（如未释放的资源）
        var expectedMemory = eventCount * 200; // 每个事件大约 200 bytes
        Assert.True(memoryDelta < expectedMemory * 2,
            $"内存泄漏检测: 在处理 {eventCount} 个事件后，内存增长异常: {memoryDelta / 1024} KB");
    }

    [Fact]
    public async Task MultipleSubscriptions_ShouldNotLeakMemory()
    {
        // Arrange
        var service = new MockHikvisionLprService();
        var subscriptionCount = 100;
        var subscriptions = new List<IDisposable>();

        // 强制 GC 并记录初始内存
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);

        _output.WriteLine($"初始内存: {initialMemory / 1024} KB");

        // Act: 创建多个 MessageBus 订阅
        for (var i = 0; i < subscriptionCount; i++)
        {
            var subscription = MessageBus.Current.Listen<LicensePlateRecognizedMessage>().Subscribe(msg =>
            {
                var _ = msg.PlateNumber;
            });
            subscriptions.Add(subscription);
        }

        // 生成一些事件
        for (var i = 0; i < 100; i++)
        {
            service.SimulatePlateRecognition($"京A{i:D5}", "Camera1", LicensePlateDirection.In);
        }

        // 释放所有订阅
        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }

        // 强制 GC 并记录最终内存
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryDelta = finalMemory - initialMemory;

        _output.WriteLine($"最终内存: {finalMemory / 1024} KB");
        _output.WriteLine($"内存变化: {memoryDelta / 1024} KB");

        // Assert: 内存应该基本恢复
        // 注意: Rx 内部可能保留一些引用，所以不应该要求完全恢复
        Assert.True(memoryDelta < 500 * 1024,
            $"内存泄漏检测: 在创建和释放 {subscriptionCount} 个订阅后，内存增长了 {memoryDelta / 1024} KB");
    }

    [Fact]
    public async Task LongRunningListener_ShouldNotLeakMemory()
    {
        // Arrange
        var service = new MockHikvisionLprService();
        var duration = TimeSpan.FromSeconds(2); // 短时间运行以避免测试时间过长
        var stopwatch = Stopwatch.StartNew();

        await service.StartAsync();

        // 订阅 MessageBus
        var subscription = MessageBus.Current.Listen<LicensePlateRecognizedMessage>().Subscribe(msg =>
        {
            var _ = msg.PlateNumber;
        });

        // 强制 GC 并记录初始内存
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);

        _output.WriteLine($"初始内存: {initialMemory / 1024} KB");

        var eventCount = 0;

        // Act: 持续生成事件（SimulatePlateRecognition 会发送 MessageBus 消息）
        while (stopwatch.Elapsed < duration)
        {
            service.SimulatePlateRecognition($"京A{eventCount:D5}", "Camera1", LicensePlateDirection.In);
            eventCount++;

            if (eventCount % 100 == 0)
            {
                // 每 100 个事件检查一次内存
                var currentMemory = GC.GetTotalMemory(false);
                _output.WriteLine($"已生成 {eventCount} 个事件, 当前内存: {currentMemory / 1024} KB");
            }
        }

        stopwatch.Stop();

        // 强制 GC 并记录最终内存
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryDelta = finalMemory - initialMemory;

        _output.WriteLine($"运行时间: {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"生成事件数: {eventCount}");
        _output.WriteLine($"最终内存: {finalMemory / 1024} KB");
        _output.WriteLine($"内存变化: {memoryDelta / 1024} KB");

        // 清理
        subscription.Dispose();
        await service.StopAsync();

        // Assert: 内存增长应该合理
        // Mock 服务会保留所有事件，所以内存增长是预期的
        var expectedMemory = eventCount * 200; // 每个事件大约 200 bytes
        Assert.True(memoryDelta < expectedMemory * 2,
            $"内存泄漏检测: 在运行 {duration.TotalSeconds} 秒并处理 {eventCount} 个事件后，内存增长异常: {memoryDelta / 1024} KB");
    }

    [Fact]
    public async Task DeviceConfigManagement_ShouldNotLeakMemory()
    {
        // Arrange
        var service = new MockHikvisionLprService();
        var deviceCount = 1000;
        var iterations = 10;

        // 强制 GC 并记录初始内存
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);

        _output.WriteLine($"初始内存: {initialMemory / 1024} KB");

        // Act: 多次添加和更新设备配置
        for (var iter = 0; iter < iterations; iter++)
        {
            for (var i = 0; i < deviceCount; i++)
            {
                var config = new LicensePlateRecognitionConfig
                {
                    Ip = $"192.168.{i / 256}.{i % 256}",
                    Name = $"Camera{i}",
                    Direction = i % 2 == 0 ? LicensePlateDirection.In : LicensePlateDirection.Out,
                    UserName = "admin",
                    Password = "admin123",
                    Port = "8000",
                    Channel = "1"
                };

                service.AddOrUpdateDevice(config);
            }

            _output.WriteLine($"完成第 {iter + 1}/{iterations} 次迭代，设备数: {service.DeviceCount}");

            // 每次迭代后清理一半设备
            if (iter > 0 && iter % 2 == 0)
            {
                // Mock 服务没有删除方法，所以这里只测试添加/更新
            }
        }

        // 强制 GC 并记录最终内存
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryDelta = finalMemory - initialMemory;

        _output.WriteLine($"最终内存: {finalMemory / 1024} KB");
        _output.WriteLine($"内存变化: {memoryDelta / 1024} KB");
        _output.WriteLine($"设备数量: {service.DeviceCount}");

        // Assert: 内存增长应该合理
        var expectedMemory = deviceCount * 500; // 每个设备配置大约 500 bytes
        Assert.True(memoryDelta < expectedMemory * 2,
            $"内存泄漏检测: 在管理 {service.DeviceCount} 个设备配置后，内存增长异常: {memoryDelta / 1024} KB");
    }

    public void Dispose()
    {
        // 清理资源
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
