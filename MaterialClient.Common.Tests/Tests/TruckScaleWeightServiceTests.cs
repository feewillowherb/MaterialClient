using System.Diagnostics;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
/// Unit tests for TruckScaleWeightService
/// Tests the lock optimization and concurrent access scenarios
/// </summary>
public class TruckScaleWeightServiceTests(ITestOutputHelper output)
{
    private readonly ISettingsService _mockSettingsService = Substitute.For<ISettingsService>();
    private readonly ILogger<TruckScaleWeightService> _mockLogger = Substitute.For<ILogger<TruckScaleWeightService>>();

    /// <summary>
    /// Test that SetWeight correctly updates weight and triggers observable
    /// </summary>
    [Fact]
    public async Task SetWeight_Should_UpdateWeight_And_TriggerObservable()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockSettingsService, _mockLogger);
        decimal? receivedWeight = null;
        var subscription = service.WeightUpdates.Subscribe(w => receivedWeight = w);

        // Act
        service.SetWeight(123.45m);
        await Task.Delay(100); // Allow observable to propagate

        // Assert
        service.GetCurrentWeight().ShouldBe(123.45m);
        receivedWeight.ShouldBe(123.45m);

        // Cleanup
        subscription.Dispose();
        await service.DisposeAsync();
    }

    /// <summary>
    /// Test concurrent read access (IsOnline property)
    /// This verifies that the read lock optimization allows multiple concurrent readers
    /// </summary>
    [Fact]
    public async Task IsOnline_Should_AllowConcurrentReads()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockSettingsService, _mockLogger);
        const int threadCount = 50;
        const int iterationsPerThread = 1000;
        var errors = 0;
        var stopwatch = Stopwatch.StartNew();

        // Act - Simulate 50 threads concurrently checking IsOnline
        var tasks = Enumerable.Range(0, threadCount).Select(async a =>
        {
            await Task.Run(() =>
            {
                for (int i = 0; i < iterationsPerThread; i++)
                {
                    try
                    {
                        _ = service.IsOnline;
                    }
                    catch
                    {
                        Interlocked.Increment(ref errors);
                    }
                }
            });
        });

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        errors.ShouldBe(0);
        var totalReads = threadCount * iterationsPerThread;
        var readsPerSecond = totalReads / stopwatch.Elapsed.TotalSeconds;

        output.WriteLine($"Concurrent reads test:");
        output.WriteLine($"  Total reads: {totalReads:N0}");
        output.WriteLine($"  Duration: {stopwatch.ElapsedMilliseconds} ms");
        output.WriteLine($"  Throughput: {readsPerSecond:N0} reads/sec");
        output.WriteLine($"  Average latency: {(stopwatch.Elapsed.TotalMilliseconds / totalReads * 1000000):N2} ns");

        // Performance expectation: should complete in less than 1 second
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000,
            $"Concurrent reads took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");

        // Cleanup
        await service.DisposeAsync();
    }

    /// <summary>
    /// Test concurrent read and write access
    /// Verifies that writes don't block readers for extended periods
    /// </summary>
    [Fact]
    public async Task ConcurrentReadWrite_Should_NotBlockReaders()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockSettingsService, _mockLogger);
        const int readerCount = 30;
        const int writerCount = 5;
        const int iterations = 100;
        var readErrors = 0;
        var writeErrors = 0;
        var readBlockCount = 0;
        var stopwatch = Stopwatch.StartNew();

        // Act - Simulate readers and writers running concurrently
        var readerTasks = Enumerable.Range(0, readerCount).Select(async _ =>
        {
            await Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    try
                    {
                        var sw = Stopwatch.StartNew();
                        var weight = service.GetCurrentWeight();
                        sw.Stop();

                        // If read took more than 1ms, it was likely blocked by a write
                        if (sw.Elapsed.TotalMilliseconds > 1)
                        {
                            Interlocked.Increment(ref readBlockCount);
                        }
                    }
                    catch
                    {
                        Interlocked.Increment(ref readErrors);
                    }
                }
            });
        });

        var writerTasks = Enumerable.Range(0, writerCount).Select(async i =>
        {
            await Task.Run(() =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    try
                    {
                        service.SetWeight((i * iterations + j) * 0.1m);
                    }
                    catch
                    {
                        Interlocked.Increment(ref writeErrors);
                    }

                    Thread.Sleep(1); // Simulate some processing time
                }
            });
        });

        await Task.WhenAll(readerTasks.Concat(writerTasks));
        stopwatch.Stop();

        // Assert
        readErrors.ShouldBe(0);
        writeErrors.ShouldBe(0);

        var totalReads = readerCount * iterations;
        var blockRate = (double)readBlockCount / totalReads * 100;

        output.WriteLine($"Concurrent read/write test:");
        output.WriteLine($"  Total reads: {totalReads:N0}");
        output.WriteLine($"  Total writes: {writerCount * iterations:N0}");
        output.WriteLine($"  Duration: {stopwatch.ElapsedMilliseconds} ms");
        output.WriteLine($"  Read block count: {readBlockCount}");
        output.WriteLine($"  Read block rate: {blockRate:F2}%");

        // After optimization, block rate should be very low (< 1%)
        blockRate.ShouldBeLessThan(1.0,
            $"Read block rate is {blockRate:F2}%, expected < 1%");

        // Cleanup
        await service.DisposeAsync();
    }

    /// <summary>
    /// Test that GetCurrentWeight doesn't block during concurrent access
    /// </summary>
    [Fact]
    public async Task GetCurrentWeight_Should_ReturnQuickly()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockSettingsService, _mockLogger);
        service.SetWeight(100.5m);
        const int iterations = 10000;
        var latencies = new long[iterations];

        // Act - Measure latency of GetCurrentWeight calls
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            _ = service.GetCurrentWeight();
            sw.Stop();
            latencies[i] = sw.Elapsed.Ticks;
        }

        // Calculate statistics
        Array.Sort(latencies);
        var p50 = latencies[iterations / 2];
        var p95 = latencies[(int)(iterations * 0.95)];
        var p99 = latencies[(int)(iterations * 0.99)];
        var avg = latencies.Average();

        var p50Ns = p50 * 1000000000.0 / Stopwatch.Frequency;
        var p95Ns = p95 * 1000000000.0 / Stopwatch.Frequency;
        var p99Ns = p99 * 1000000000.0 / Stopwatch.Frequency;
        var avgNs = avg * 1000000000.0 / Stopwatch.Frequency;

        output.WriteLine($"GetCurrentWeight latency:");
        output.WriteLine($"  P50: {p50Ns:N0} ns");
        output.WriteLine($"  P95: {p95Ns:N0} ns");
        output.WriteLine($"  P99: {p99Ns:N0} ns");
        output.WriteLine($"  Avg: {avgNs:N0} ns");

        // Assert - P99 should be less than 1 microsecond (1000 ns)
        p99Ns.ShouldBeLessThan(1000,
            $"P99 latency is {p99Ns:N0}ns, expected < 1000ns");

        // Cleanup
        await service.DisposeAsync();
    }

    /// <summary>
    /// Test that multiple SetWeight calls don't cause deadlock
    /// </summary>
    [Fact]
    public async Task SetWeight_ConcurrentCalls_Should_NotDeadlock()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockSettingsService, _mockLogger);
        const int threadCount = 10;
        const int iterationsPerThread = 100;
        var errors = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act - Multiple threads writing concurrently
        var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            await Task.Run(() =>
            {
                for (int i = 0; i < iterationsPerThread && !cts.Token.IsCancellationRequested; i++)
                {
                    try
                    {
                        service.SetWeight((threadId * iterationsPerThread + i) * 0.1m);
                    }
                    catch
                    {
                        Interlocked.Increment(ref errors);
                    }
                }
            }, cts.Token);
        });

        var allTasks = Task.WhenAll(tasks);
        var completedTask = await Task.WhenAny(allTasks, Task.Delay(5000));

        // Assert
        (completedTask == allTasks).ShouldBeTrue("SetWeight operations timed out (possible deadlock)");
        errors.ShouldBe(0);

        // Cleanup
        await service.DisposeAsync();
    }

    /// <summary>
    /// Test WeightUpdates observable stream
    /// </summary>
    [Fact]
    public async Task WeightUpdates_Should_EmitAllUpdates()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockSettingsService, _mockLogger);
        var receivedWeights = new System.Collections.Concurrent.ConcurrentBag<decimal>();
        var subscription = service.WeightUpdates.Subscribe(w => receivedWeights.Add(w));

        // Act
        var expectedWeights = new[] { 10.5m, 20.3m, 30.7m, 40.2m, 50.9m };
        foreach (var weight in expectedWeights)
        {
            service.SetWeight(weight);
            await Task.Delay(10); // Small delay to allow observable to propagate
        }

        await Task.Delay(100); // Wait for all observables to complete

        // Assert
        receivedWeights.Count.ShouldBe(expectedWeights.Length);
        foreach (var expected in expectedWeights)
        {
            receivedWeights.ShouldContain(expected);
        }

        // Cleanup
        subscription.Dispose();
        await service.DisposeAsync();
    }

    /// <summary>
    /// Test that IsOnline returns false when service is not initialized
    /// </summary>
    [Fact]
    public async Task IsOnline_Should_ReturnFalse_WhenNotInitialized()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockSettingsService, _mockLogger);

        // Act
        var isOnline = service.IsOnline;

        // Assert
        isOnline.ShouldBeFalse();

        // Cleanup
        await service.DisposeAsync();
    }

    /// <summary>
    /// Test concurrent access to IsOnline and SetWeight
    /// </summary>
    [Fact]
    public async Task IsOnline_And_SetWeight_Should_NotInterfere()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockSettingsService, _mockLogger);
        const int duration = 1000; // 1 second
        var errors = 0;
        var readCount = 0;
        var writeCount = 0;
        var cts = new CancellationTokenSource(duration);

        // Act - Readers and writers running simultaneously
        var readerTask = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    _ = service.IsOnline;
                    Interlocked.Increment(ref readCount);
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            }
        });

        var writerTask = Task.Run(() =>
        {
            decimal weight = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    service.SetWeight(weight++);
                    Interlocked.Increment(ref writeCount);
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }

                Thread.Sleep(1); // Throttle writes
            }
        });

        await Task.WhenAll(readerTask, writerTask);

        // Assert
        errors.ShouldBe(0);
        output.WriteLine($"IsOnline/SetWeight interference test:");
        output.WriteLine($"  Read operations: {readCount:N0}");
        output.WriteLine($"  Write operations: {writeCount:N0}");
        output.WriteLine($"  Errors: {errors}");

        // Should have high throughput for reads
        readCount.ShouldBeGreaterThan(10000, $"Read count {readCount} is too low");

        // Cleanup
        await service.DisposeAsync();
    }

    /// <summary>
    /// Stress test: high-frequency concurrent operations
    /// </summary>
    [Fact]
    public async Task StressTest_HighFrequency_ConcurrentOperations()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockSettingsService, _mockLogger);
        const int readerCount = 20;
        const int writerCount = 5;
        const int duration = 2000; // 2 seconds
        var readCount = 0;
        var writeCount = 0;
        var errors = 0;
        var cts = new CancellationTokenSource(duration);
        var stopwatch = Stopwatch.StartNew();

        // Act - High-frequency operations
        var readerTasks = Enumerable.Range(0, readerCount).Select(a => Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    _  = service.GetCurrentWeight();
                    _  = service.IsOnline;
                    Interlocked.Increment(ref readCount);
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            }
        }));

        var writerTasks = Enumerable.Range(0, writerCount).Select(_ => Task.Run(() =>
        {
            decimal weight = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    service.SetWeight(weight++);
                    Interlocked.Increment(ref writeCount);
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }

                Thread.Sleep(1); // Simulate data arrival rate
            }
        }));

        await Task.WhenAll(readerTasks.Concat(writerTasks));
        stopwatch.Stop();

        // Assert
        errors.ShouldBe(0);

        var readsPerSecond = readCount / stopwatch.Elapsed.TotalSeconds;
        var writesPerSecond = writeCount / stopwatch.Elapsed.TotalSeconds;

        output.WriteLine($"Stress test results:");
        output.WriteLine($"  Duration: {stopwatch.ElapsedMilliseconds} ms");
        output.WriteLine($"  Total reads: {readCount:N0}");
        output.WriteLine($"  Total writes: {writeCount:N0}");
        output.WriteLine($"  Reads/sec: {readsPerSecond:N0}");
        output.WriteLine($"  Writes/sec: {writesPerSecond:N0}");
        output.WriteLine($"  Errors: {errors}");

        // Performance expectations
        readsPerSecond.ShouldBeGreaterThan(50000,
            $"Read throughput {readsPerSecond:N0}/sec is too low, expected > 50,000/sec");

        // Cleanup
        await service.DisposeAsync();
    }

    /// <summary>
    /// Test that DisposeAsync properly cleans up resources
    /// </summary>
    [Fact]
    public async Task DisposeAsync_Should_CleanupResources()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockSettingsService, _mockLogger);
        var receivedWeights = new System.Collections.Concurrent.ConcurrentBag<decimal>();
        var subscription = service.WeightUpdates.Subscribe(w => receivedWeights.Add(w));

        service.SetWeight(123.45m);
        await Task.Delay(100);

        // Act
        await service.DisposeAsync();

        // Assert
        // After disposal, operations should not crash but may not work
        // This is a basic cleanup test
        receivedWeights.Count.ShouldBe(1);

        subscription.Dispose();
    }
}