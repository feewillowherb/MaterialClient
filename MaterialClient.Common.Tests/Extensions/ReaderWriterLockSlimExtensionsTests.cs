using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MaterialClient.Common.Extensions;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Extensions;

/// <summary>
/// Performance tests for ReaderWriterLockSlim extensions
/// Verifies zero-allocation struct disposables and performance characteristics
/// </summary>
public class ReaderWriterLockSlimExtensionsTests(ITestOutputHelper output)
{
    /// <summary>
    /// Test basic read lock functionality
    /// </summary>
    [Fact]
    public void ReadLock_Should_AcquireAndRelease()
    {
        // Arrange
        var rwLock = new ReaderWriterLockSlim();
        var sharedValue = 0;

        // Act
        using (rwLock.ReadLock())
        {
            rwLock.IsReadLockHeld.ShouldBeTrue();
            _ = sharedValue; // Read operation
        }

        // Assert
        rwLock.IsReadLockHeld.ShouldBeFalse();
        rwLock.Dispose();
    }

    /// <summary>
    /// Test basic write lock functionality
    /// </summary>
    [Fact]
    public void WriteLock_Should_AcquireAndRelease()
    {
        // Arrange
        var rwLock = new ReaderWriterLockSlim();
        var sharedValue = 0;

        // Act
        using (rwLock.WriteLock())
        {
            rwLock.IsWriteLockHeld.ShouldBeTrue();
            sharedValue = 42; // Write operation
        }

        // Assert
        rwLock.IsWriteLockHeld.ShouldBeFalse();
        sharedValue.ShouldBe(42);
        rwLock.Dispose();
    }

    /// <summary>
    /// Test upgradeable read lock functionality
    /// </summary>
    [Fact]
    public void UpgradeableReadLock_Should_AcquireAndRelease()
    {
        // Arrange
        var rwLock = new ReaderWriterLockSlim();
        var sharedValue = 0;

        // Act
        using (rwLock.UpgradeableReadLock())
        {
            rwLock.IsUpgradeableReadLockHeld.ShouldBeTrue();
            
            if (sharedValue == 0)
            {
                using (rwLock.WriteLock())
                {
                    sharedValue = 42;
                }
            }
        }

        // Assert
        rwLock.IsUpgradeableReadLockHeld.ShouldBeFalse();
        sharedValue.ShouldBe(42);
        rwLock.Dispose();
    }

    /// <summary>
    /// Test concurrent read lock acquisition
    /// </summary>
    [Fact]
    public async Task ReadLock_Should_AllowConcurrentReaders()
    {
        // Arrange
        var rwLock = new ReaderWriterLockSlim();
        const int readerCount = 50;
        var concurrentReaders = 0;
        var maxConcurrentReaders = 0;

        // Act
        var tasks = Enumerable.Range(0, readerCount).Select(async _ =>
        {
            await Task.Run(() =>
            {
                using (rwLock.ReadLock())
                {
                    var current = Interlocked.Increment(ref concurrentReaders);
                    var max = maxConcurrentReaders;
                    while (current > max)
                    {
                        max = Interlocked.CompareExchange(ref maxConcurrentReaders, current, max);
                        if (max >= current) break;
                    }
                    
                    Thread.Sleep(10); // Hold lock briefly
                    Interlocked.Decrement(ref concurrentReaders);
                }
            });
        });

        await Task.WhenAll(tasks);

        // Assert
        maxConcurrentReaders.ShouldBeGreaterThan(1, 
            $"Expected multiple concurrent readers, got {maxConcurrentReaders}");
        
        output.WriteLine($"Max concurrent readers: {maxConcurrentReaders}");
        rwLock.Dispose();
    }

    /// <summary>
    /// Test write lock exclusivity
    /// </summary>
    [Fact]
    public async Task WriteLock_Should_BeExclusive()
    {
        // Arrange
        var rwLock = new ReaderWriterLockSlim();
        var sharedValue = 0;
        var concurrentWrites = 0;
        var maxConcurrentWrites = 0;

        // Act
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            await Task.Run(() =>
            {
                using (rwLock.WriteLock())
                {
                    var current = Interlocked.Increment(ref concurrentWrites);
                    var max = maxConcurrentWrites;
                    while (current > max)
                    {
                        max = Interlocked.CompareExchange(ref maxConcurrentWrites, current, max);
                        if (max >= current) break;
                    }
                    
                    sharedValue = i;
                    Thread.Sleep(5);
                    Interlocked.Decrement(ref concurrentWrites);
                }
            });
        });

        await Task.WhenAll(tasks);

        // Assert
        maxConcurrentWrites.ShouldBe(1); // Only 1 writer at a time
        output.WriteLine($"Max concurrent writes: {maxConcurrentWrites}");
        rwLock.Dispose();
    }

    /// <summary>
    /// Performance test: measure read lock latency
    /// </summary>
    [Fact]
    public void ReadLock_Performance_Should_BeFast()
    {
        // Arrange
        var rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        const int iterations = 100000;
        var latencies = new long[iterations];

        // Warmup
        for (int i = 0; i < 1000; i++)
        {
            using var _ = rwLock.ReadLock();
        }

        // Act - Measure latency
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            using (rwLock.ReadLock())
            {
                // Minimal work
            }
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

        output.WriteLine($"ReadLock latency ({iterations:N0} iterations):");
        output.WriteLine($"  P50: {p50Ns:N0} ns");
        output.WriteLine($"  P95: {p95Ns:N0} ns");
        output.WriteLine($"  P99: {p99Ns:N0} ns");
        output.WriteLine($"  Avg: {avgNs:N0} ns");

        // Assert - P99 should be less than 500ns for uncontended lock
        p99Ns.ShouldBeLessThan(500, 
            $"P99 latency {p99Ns:N0}ns is too high, expected < 500ns");

        rwLock.Dispose();
    }

    /// <summary>
    /// Performance test: measure write lock latency
    /// </summary>
    [Fact]
    public void WriteLock_Performance_Should_BeFast()
    {
        // Arrange
        var rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        const int iterations = 100000;
        var latencies = new long[iterations];

        // Warmup
        for (int i = 0; i < 1000; i++)
        {
            using var _ = rwLock.WriteLock();
        }

        // Act - Measure latency
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            using (rwLock.WriteLock())
            {
                // Minimal work
            }
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

        output.WriteLine($"WriteLock latency ({iterations:N0} iterations):");
        output.WriteLine($"  P50: {p50Ns:N0} ns");
        output.WriteLine($"  P95: {p95Ns:N0} ns");
        output.WriteLine($"  P99: {p99Ns:N0} ns");
        output.WriteLine($"  Avg: {avgNs:N0} ns");

        // Assert - P99 should be less than 500ns for uncontended lock
        p99Ns.ShouldBeLessThan(500, 
            $"P99 latency {p99Ns:N0}ns is too high, expected < 500ns");

        rwLock.Dispose();
    }

    /// <summary>
    /// Stress test: concurrent readers and writers
    /// </summary>
    [Fact]
    public async Task StressTest_ConcurrentReadersAndWriters()
    {
        // Arrange
        var rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        var sharedValue = 0;
        const int readerCount = 30;
        const int writerCount = 5;
        const int duration = 2000; // 2 seconds
        var readOps = 0;
        var writeOps = 0;
        var errors = 0;
        var cts = new CancellationTokenSource(duration);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var readerTasks = Enumerable.Range(0, readerCount).Select(_ => Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    using (rwLock.ReadLock())
                    {
                        _ = sharedValue;
                        Interlocked.Increment(ref readOps);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            }
        }));

        var writerTasks = Enumerable.Range(0, writerCount).Select(_ => Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    using (rwLock.WriteLock())
                    {
                        sharedValue++;
                        Interlocked.Increment(ref writeOps);
                    }
                    Thread.Sleep(1); // Simulate processing
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            }
        }));

        await Task.WhenAll(readerTasks.Concat(writerTasks));
        stopwatch.Stop();

        // Assert
        errors.ShouldBe(0);
        sharedValue.ShouldBe(writeOps);
        
        var readsPerSec = readOps / stopwatch.Elapsed.TotalSeconds;
        var writesPerSec = writeOps / stopwatch.Elapsed.TotalSeconds;
        
        output.WriteLine($"Stress test results:");
        output.WriteLine($"  Duration: {stopwatch.ElapsedMilliseconds} ms");
        output.WriteLine($"  Read operations: {readOps:N0} ({readsPerSec:N0}/sec)");
        output.WriteLine($"  Write operations: {writeOps:N0} ({writesPerSec:N0}/sec)");
        output.WriteLine($"  Errors: {errors}");

        // Performance expectations
        readsPerSec.ShouldBeGreaterThan(100000, 
            $"Read throughput {readsPerSec:N0}/sec is too low");

        rwLock.Dispose();
    }

    /// <summary>
    /// Test that NoRecursion policy is faster than SupportsRecursion
    /// </summary>
    [Fact]
    public void NoRecursion_Should_BeFaster_ThanSupportsRecursion()
    {
        // Arrange
        var noRecursionLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        var supportsRecursionLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        const int iterations = 100000;

        // Warmup
        for (int i = 0; i < 1000; i++)
        {
            using var _ = noRecursionLock.ReadLock();
            using var __ = supportsRecursionLock.ReadLock();
        }

        // Act - Measure NoRecursion
        var sw1 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            using var _ = noRecursionLock.ReadLock();
        }
        sw1.Stop();

        // Act - Measure SupportsRecursion
        var sw2 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            using var _ = supportsRecursionLock.ReadLock();
        }
        sw2.Stop();

        var noRecursionNs = sw1.Elapsed.TotalMilliseconds * 1000000 / iterations;
        var supportsRecursionNs = sw2.Elapsed.TotalMilliseconds * 1000000 / iterations;
        var improvement = (supportsRecursionNs - noRecursionNs) / supportsRecursionNs * 100;

        output.WriteLine($"Recursion policy comparison ({iterations:N0} iterations):");
        output.WriteLine($"  NoRecursion:        {noRecursionNs:N0} ns/op");
        output.WriteLine($"  SupportsRecursion:  {supportsRecursionNs:N0} ns/op");
        output.WriteLine($"  Improvement:        {improvement:F1}%");

        // Assert - NoRecursion should be at least 10% faster
        improvement.ShouldBeGreaterThan(10, 
            $"NoRecursion improvement {improvement:F1}% is less than expected 10%");

        noRecursionLock.Dispose();
        supportsRecursionLock.Dispose();
    }

    /// <summary>
    /// Test exception safety - lock should be released even on exception
    /// </summary>
    [Fact]
    public void Lock_Should_BeReleased_OnException()
    {
        // Arrange
        var rwLock = new ReaderWriterLockSlim();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
        {
            using (rwLock.WriteLock())
            {
                throw new InvalidOperationException("Test exception");
            }
        });

        // Lock should be released
        rwLock.IsWriteLockHeld.ShouldBeFalse();
        
        // Should be able to acquire lock again
        using (rwLock.WriteLock())
        {
            rwLock.IsWriteLockHeld.ShouldBeTrue();
        }

        rwLock.Dispose();
    }
}

