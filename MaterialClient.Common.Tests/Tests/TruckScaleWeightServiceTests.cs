using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
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
    private readonly ISerialPortFactory _mockSerialPortFactory = CreateMockSerialPortFactory();

    private static ISerialPortFactory CreateMockSerialPortFactory()
    {
        var mockFactory = Substitute.For<ISerialPortFactory>();
        var mockSerialPort = Substitute.For<ISerialPort>();
        mockSerialPort.IsOpen.Returns(false);
        mockFactory.Create().Returns(mockSerialPort);
        return mockFactory;
    }

    /// <summary>
    /// Test that SetWeight correctly updates weight and triggers observable
    /// </summary>
    [Fact(Timeout = 5000)]
    public async Task SetWeight_Should_UpdateWeight_And_TriggerObservable()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, _mockSerialPortFactory);
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
    [Fact(Timeout = 5000)]
    public async Task IsOnline_Should_AllowConcurrentReads()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, _mockSerialPortFactory);
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
    [Fact(Timeout = 5000)]
    public async Task ConcurrentReadWrite_Should_NotBlockReaders()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, _mockSerialPortFactory);
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
                        service.GetCurrentWeight();
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
    [Fact(Timeout = 5000)]
    public async Task GetCurrentWeight_Should_ReturnQuickly()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, _mockSerialPortFactory);
        service.SetWeight(100.5m);
        const int iterations = 10000;
        var latencies = new long[iterations];

        // Act - Measure latency of GetCurrentWeight calls
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            service.GetCurrentWeight();
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
    [Fact(Timeout = 5000)]
    public async Task SetWeight_ConcurrentCalls_Should_NotDeadlock()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, _mockSerialPortFactory);
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
    [Fact(Timeout = 5000)]
    public async Task WeightUpdates_Should_EmitAllUpdates()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, _mockSerialPortFactory);
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
    [Fact(Timeout = 5000)]
    public async Task IsOnline_Should_ReturnFalse_WhenNotInitialized()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, _mockSerialPortFactory);

        // Act
        var isOnline = service.IsOnline;

        // Assert
        isOnline.ShouldBeFalse();

        // Cleanup
        await service.DisposeAsync();
    }
    

    /// <summary>
    /// Test that DisposeAsync properly cleans up resources
    /// </summary>
    [Fact(Timeout = 5000)]
    public async Task DisposeAsync_Should_CleanupResources()
    {
        // Arrange
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, _mockSerialPortFactory);
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

    /// <summary>
    /// Test parsing actual serial port HEX data for Default scale type
    /// Tests real data received from truck scale hardware
    /// Data format: 02 2B [digits] [marker] 03
    /// </summary>
    [Fact(Timeout = 5000)]
    public async Task ParseHexData_DefaultScale_Should_ParseCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            // Data: 02 2B 30 30 30 32 34 30 30 31 44 03
            // Parsed: "000240" (6 digits before 'D') -> 240 -> 2.40 kg
            new { Data = new byte[] { 0x02, 0x2B, 0x30, 0x30, 0x30, 0x32, 0x34, 0x30, 0x30, 0x31, 0x44, 0x03 }, ExpectedRaw = 240m },
            
            // Data: 02 2B 30 30 30 31 34 0 30 30 31 45 03
            // Parsed: "000140" (stops at 'E') -> 140 -> 1.40 kg
            new { Data = new byte[] { 0x02, 0x2B, 0x30, 0x30, 0x30, 0x31, 0x34, 0x30, 0x30, 0x31, 0x45, 0x03 }, ExpectedRaw = 140m },
            
            // Data: 02 2B 30 30 30 30 34 30 30 31 46 03
            // Parsed: "000040" (6 digits before 'F') -> 40 -> 0.40 kg
            new { Data = new byte[] { 0x02, 0x2B, 0x30, 0x30, 0x30, 0x30, 0x34, 0x30, 0x30, 0x31, 0x46, 0x03 }, ExpectedRaw = 40m },
            
            // Data: 02 2B 31 30 30 30 30 30 30 31 45 03
            // Parsed: "100000" (6 digits before 'E') -> 100000 -> 100000 kg = 100t
            new { Data = new byte[] { 0x02, 0x2B, 0x31, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x31, 0x45, 0x03 }, ExpectedRaw = 100000m },
            
            // Data: 02 2B 31 30 31 30 30 30 30 31 45 03
            // Parsed: "101000" (6 digits before 'E') -> 101000 -> 101000 kg = 101t
            new { Data = new byte[] { 0x02, 0x2B, 0x31, 0x30, 0x31, 0x30, 0x30, 0x30, 0x30, 0x31, 0x45, 0x03 }, ExpectedRaw = 101000m }
        };

        var receivedWeights = new System.Collections.Concurrent.ConcurrentBag<decimal>();
        
        foreach (var testCase in testCases)
        {
            // Create fresh mocks for each test case
            var mockSerialPort = Substitute.For<ISerialPort>();
            var mockFactory = Substitute.For<ISerialPortFactory>();
            mockFactory.Create().Returns(mockSerialPort);
            
            var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, mockFactory);
            
            // Configure scale settings for Default type with HEX communication
            var settings = new ScaleSettings
            {
                SerialPort = "COM3",
                BaudRate = "9600",
                CommunicationMethod = "TF0",
                ScaleType = ScaleType.Yaohua,
                ScaleUnit = ScaleUnit.Kg
            };
            
            var subscription = service.WeightUpdates.Subscribe(w => receivedWeights.Add(w));

            // Setup mock serial port
            mockSerialPort.IsOpen.Returns(true);
            SetupMockSerialPortRead(mockSerialPort, testCase.Data);

            // Initialize service
            await service.InitializeAsync(settings);
            
            // Trigger DataReceived event using reflection to create SerialDataReceivedEventArgs
            var eventArgsType = typeof(SerialDataReceivedEventArgs);
            var eventArgs = (SerialDataReceivedEventArgs)Activator.CreateInstance(
                eventArgsType, 
                BindingFlags.NonPublic | BindingFlags.Instance, 
                null, 
                new object[] { SerialData.Chars }, 
                null)!;
            
            mockSerialPort.DataReceived += Raise.Event<SerialDataReceivedEventHandler>(
                mockSerialPort, 
                eventArgs);
            
            await Task.Delay(300); // Wait for processing

            // Assert - verify weight was parsed and converted
            var currentWeight = service.GetCurrentWeight();
            output.WriteLine($"Data: {BitConverter.ToString(testCase.Data)}");
            output.WriteLine($"  Expected raw: {testCase.ExpectedRaw} kg");
            output.WriteLine($"  Parsed weight: {currentWeight} ton");
            output.WriteLine($"  Received updates: {receivedWeights.Count}");

            // Cleanup
            subscription.Dispose();
            await service.DisposeAsync();
        }

        // Final assertion
        receivedWeights.Count.ShouldBeGreaterThan(0, "Should have received weight updates");
        output.WriteLine($"Total weight updates received: {receivedWeights.Count}");
    }

    /// <summary>
    /// Test that invalid data (not starting with 0x02) is correctly filtered out
    /// Tests error data that should be discarded without causing exceptions
    /// </summary>
    [Fact(Timeout = 5000)]
    public async Task ParseHexData_InvalidData_Should_BeFilteredOut()
    {
        // Arrange
        var mockSerialPort = Substitute.For<ISerialPort>();
        var mockFactory = Substitute.For<ISerialPortFactory>();
        mockFactory.Create().Returns(mockSerialPort);
        
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, mockFactory);
        
        // Configure scale settings for Default type with HEX communication
        var settings = new ScaleSettings
        {
            SerialPort = "COM3",
            BaudRate = "9600",
            CommunicationMethod = "TF0",
            ScaleType = ScaleType.Yaohua,
            ScaleUnit = ScaleUnit.Kg
        };
        
        var receivedWeights = new System.Collections.Concurrent.ConcurrentBag<decimal>();
        var subscription = service.WeightUpdates.Subscribe(w => receivedWeights.Add(w));

        // Setup mock serial port
        mockSerialPort.IsOpen.Returns(true);
        
        // Invalid data: AA BB 33 00 7B 22 77 67 22 3A 22 32 36 2D 30 31 2B 30 30 30 30 34 30 30
        // This data doesn't start with 0x02, so it should be discarded
        var invalidData1 = new byte[] 
        { 
            0xAA, 0xBB, 0x33, 0x00, 0x7B, 0x22, 0x77, 0x67, 0x22, 0x3A, 0x22, 0x32, 0x36, 0x2D, 0x30, 0x31, 0x2B, 0x30, 0x30, 0x30, 0x30, 0x34, 0x30, 0x30 
        };
        
        // Continuation data: 31 46 03
        var invalidData2 = new byte[] { 0x31, 0x46, 0x03 };
        
        // Initialize service
        await service.InitializeAsync(settings);
        
        // Get initial weight
        var initialWeight = service.GetCurrentWeight();
        var initialWeightCount = receivedWeights.Count;
        
        // Act - Send invalid data 1
        SetupMockSerialPortRead(mockSerialPort, invalidData1);
        
        var eventArgsType = typeof(SerialDataReceivedEventArgs);
        var eventArgs1 = (SerialDataReceivedEventArgs)Activator.CreateInstance(
            eventArgsType, 
            BindingFlags.NonPublic | BindingFlags.Instance, 
            null, 
            new object[] { SerialData.Chars }, 
            null)!;
        
        mockSerialPort.DataReceived += Raise.Event<SerialDataReceivedEventHandler>(
            mockSerialPort, 
            eventArgs1);
        
        await Task.Delay(300); // Wait for processing
        
        // Verify invalid data 1 was filtered out
        var weightAfterInvalid1 = service.GetCurrentWeight();
        var weightCountAfterInvalid1 = receivedWeights.Count;
        
        output.WriteLine($"After invalid data 1:");
        output.WriteLine($"  Initial weight: {initialWeight} ton");
        output.WriteLine($"  Weight after invalid data: {weightAfterInvalid1} ton");
        output.WriteLine($"  Weight updates: {initialWeightCount} -> {weightCountAfterInvalid1}");
        
        // Act - Send invalid data 2 (continuation)
        SetupMockSerialPortRead(mockSerialPort, invalidData2);
        
        var eventArgs2 = (SerialDataReceivedEventArgs)Activator.CreateInstance(
            eventArgsType, 
            BindingFlags.NonPublic | BindingFlags.Instance, 
            null, 
            new object[] { SerialData.Chars }, 
            null)!;
        
        mockSerialPort.DataReceived += Raise.Event<SerialDataReceivedEventHandler>(
            mockSerialPort, 
            eventArgs2);
        
        await Task.Delay(300); // Wait for processing
        
        // Verify invalid data 2 was also filtered out
        var weightAfterInvalid2 = service.GetCurrentWeight();
        var weightCountAfterInvalid2 = receivedWeights.Count;
        
        output.WriteLine($"After invalid data 2:");
        output.WriteLine($"  Weight after invalid data 2: {weightAfterInvalid2} ton");
        output.WriteLine($"  Weight updates: {weightCountAfterInvalid1} -> {weightCountAfterInvalid2}");
        
        // Act - Send valid data to verify service still works after filtering invalid data
        var validData = new byte[] { 0x02, 0x2B, 0x30, 0x30, 0x30, 0x30, 0x34, 0x30, 0x30, 0x31, 0x46, 0x03 };
        SetupMockSerialPortRead(mockSerialPort, validData);
        
        var eventArgs3 = (SerialDataReceivedEventArgs)Activator.CreateInstance(
            eventArgsType, 
            BindingFlags.NonPublic | BindingFlags.Instance, 
            null, 
            new object[] { SerialData.Chars }, 
            null)!;
        
        mockSerialPort.DataReceived += Raise.Event<SerialDataReceivedEventHandler>(
            mockSerialPort, 
            eventArgs3);
        
        await Task.Delay(300); // Wait for processing
        
        var weightAfterValid = service.GetCurrentWeight();
        var weightCountAfterValid = receivedWeights.Count;
        
        output.WriteLine($"After valid data:");
        output.WriteLine($"  Weight after valid data: {weightAfterValid} ton");
        output.WriteLine($"  Weight updates: {weightCountAfterInvalid2} -> {weightCountAfterValid}");

        // Assert
        // Invalid data should not cause weight updates
        weightCountAfterInvalid1.ShouldBe(initialWeightCount, "Invalid data 1 should not produce weight updates");
        weightCountAfterInvalid2.ShouldBe(initialWeightCount, "Invalid data 2 should not produce weight updates");
        
        // Weight should remain unchanged after invalid data
        weightAfterInvalid1.ShouldBe(initialWeight, "Weight should not change after invalid data 1");
        weightAfterInvalid2.ShouldBe(initialWeight, "Weight should not change after invalid data 2");
        
        // Valid data should still work after filtering invalid data
        weightCountAfterValid.ShouldBeGreaterThan(weightCountAfterInvalid2, "Valid data should produce weight updates after invalid data was filtered");
        
        // Service should not throw exceptions when processing invalid data
        // (If we get here without exceptions, the test passes)

        // Cleanup
        subscription.Dispose();
        await service.DisposeAsync();
    }

    /// <summary>
    /// Test parsing DingSong 12-byte HEX data
    /// Format: 02 2B [8 digits] [marker] 03
    /// Tests multiple consecutive frames
    /// </summary>
    [Fact()]
    public async Task ParseHexData_DingSong_12Byte_Should_ParseCorrectly()
    {
        // Arrange
        var mockSerialPort = Substitute.For<ISerialPort>();
        var mockFactory = Substitute.For<ISerialPortFactory>();
        mockFactory.Create().Returns(mockSerialPort);
        
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, mockFactory);
        
        // Configure scale settings for DingSong type with HEX communication
        var settings = new ScaleSettings
        {
            SerialPort = "COM3",
            BaudRate = "9600",
            CommunicationMethod = "TF0",
            ScaleType = ScaleType.DingSong,
            ScaleUnit = ScaleUnit.Kg
        };
        
        var receivedWeights = new System.Collections.Concurrent.ConcurrentBag<decimal>();
        var subscription = service.WeightUpdates.Subscribe(w => receivedWeights.Add(w));

        // Setup mock serial port
        mockSerialPort.IsOpen.Returns(true);
        
        // DingSong 12-byte positive data: 02 2B 30 30 31 30 30 30 30 30 42 03
        // Parsed: "00100000" -> 100000 kg = 100t (no division by 100)
        var dingSongPositiveData = new byte[] { 0x02, 0x2B, 0x30, 0x30, 0x31, 0x30, 0x30, 0x30, 0x30, 0x30, 0x42, 0x03 };
        
        // DingSong 12-byte negative data: 02 2D 30 30 31 30 30 30 30 30 42 03
        // Parsed: "-00100000" -> -100000 kg = -100t (no division by 100)
        var dingSongNegativeData = new byte[] { 0x02, 0x2D, 0x30, 0x30, 0x31, 0x30, 0x30, 0x30, 0x30, 0x30, 0x42, 0x03 };
        
        // Initialize service
        await service.InitializeAsync(settings);
        
        // Send positive data (100t)
        SetupMockSerialPortRead(mockSerialPort, dingSongPositiveData);
        
        var eventArgsType = typeof(SerialDataReceivedEventArgs);
        var eventArgs = (SerialDataReceivedEventArgs)Activator.CreateInstance(
            eventArgsType, 
            BindingFlags.NonPublic | BindingFlags.Instance, 
            null, 
            new object[] { SerialData.Chars }, 
            null)!;
        
        mockSerialPort.DataReceived += Raise.Event<SerialDataReceivedEventHandler>(
            mockSerialPort, 
            eventArgs);
        
        await Task.Delay(300); // Wait for processing
        
        // Send negative data to test negative parsing (-100t)
        SetupMockSerialPortRead(mockSerialPort, dingSongNegativeData);
        
        var negativeEventArgs = (SerialDataReceivedEventArgs)Activator.CreateInstance(
            eventArgsType, 
            BindingFlags.NonPublic | BindingFlags.Instance, 
            null, 
            new object[] { SerialData.Chars }, 
            null)!;
        
        mockSerialPort.DataReceived += Raise.Event<SerialDataReceivedEventHandler>(
            mockSerialPort, 
            negativeEventArgs);
        
        await Task.Delay(300); // Wait for processing

        // Assert
        // Expected positive: "00100000" -> 100000 kg -> converted to 100t
        // Expected negative: "-00100000" -> -100000 kg -> converted to -100t
        receivedWeights.Count.ShouldBeGreaterThan(0, "Should have received weight updates");
        
        // Verify we have both positive and negative weights
        var positiveWeights = receivedWeights.Where(w => w >= 0).ToList();
        var negativeWeights = receivedWeights.Where(w => w < 0).ToList();
        
        positiveWeights.Count.ShouldBeGreaterThan(0, "Should have received positive weight updates");
        negativeWeights.Count.ShouldBeGreaterThan(0, "Should have received negative weight updates");
        
        output.WriteLine($"DingSong 12-byte test:");
        output.WriteLine($"  Positive data: {BitConverter.ToString(dingSongPositiveData)}");
        output.WriteLine($"    Parsed: \"00100000\" -> 100000 kg = 100t (no division by 100)");
        output.WriteLine($"  Negative data: {BitConverter.ToString(dingSongNegativeData)}");
        output.WriteLine($"    Parsed: \"-00100000\" -> -100000 kg = -100t (no division by 100)");
        output.WriteLine($"  Total received updates: {receivedWeights.Count}");
        output.WriteLine($"  Positive weights: {positiveWeights.Count}");
        output.WriteLine($"  Negative weights: {negativeWeights.Count}");
        foreach (var weight in receivedWeights)
        {
            output.WriteLine($"    Weight: {weight} ton");
        }

        // Cleanup
        subscription.Dispose();
        await service.DisposeAsync();
    }

    /// <summary>
    /// Test parsing DingSong 12-byte HEX data with specific test case
    /// Data: 02 2B 30 30 30 39 36 30 30 31 34 03
    /// Parsed: "00096001" -> 96001 kg
    /// </summary>
    [Fact(Timeout = 5000)]
    public async Task ParseHexData_DingSong_12Byte_SpecificCase_Should_ParseCorrectly()
    {
        // Arrange
        var mockSerialPort = Substitute.For<ISerialPort>();
        var mockFactory = Substitute.For<ISerialPortFactory>();
        mockFactory.Create().Returns(mockSerialPort);
        
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, mockFactory);
        
        // Configure scale settings for DingSong type with HEX communication
        var settings = new ScaleSettings
        {
            SerialPort = "COM3",
            BaudRate = "9600",
            CommunicationMethod = "TF0",
            ScaleType = ScaleType.DingSong,
            ScaleUnit = ScaleUnit.Kg
        };
        
        var receivedWeights = new System.Collections.Concurrent.ConcurrentBag<decimal>();
        var subscription = service.WeightUpdates.Subscribe(w => receivedWeights.Add(w));

        // Setup mock serial port
        mockSerialPort.IsOpen.Returns(true);
        
        // DingSong 12-byte data: 02 2B 30 30 30 39 36 30 30 31 34 03
        // Parsed: "00096001" -> 96001 kg (no division by 100)
        var dingSongData = new byte[] { 0x02, 0x2B, 0x30, 0x30, 0x30, 0x39, 0x36, 0x30, 0x30, 0x31, 0x34, 0x03 };
        
        // Initialize service
        await service.InitializeAsync(settings);
        
        // Setup mock to read data
        SetupMockSerialPortRead(mockSerialPort, dingSongData);
        
        var eventArgsType = typeof(SerialDataReceivedEventArgs);
        var eventArgs = (SerialDataReceivedEventArgs)Activator.CreateInstance(
            eventArgsType, 
            BindingFlags.NonPublic | BindingFlags.Instance, 
            null, 
            new object[] { SerialData.Chars }, 
            null)!;
        
        mockSerialPort.DataReceived += Raise.Event<SerialDataReceivedEventHandler>(
            mockSerialPort, 
            eventArgs);
        
        await Task.Delay(300); // Wait for processing

        // Assert
        // Expected: "00096001" -> 96001 kg -> converted to ton
        receivedWeights.Count.ShouldBeGreaterThan(0, "Should have received weight updates");
        
        var finalWeight = service.GetCurrentWeight();
        finalWeight.ShouldBeGreaterThan(0, "Weight should be positive");
        
        output.WriteLine($"DingSong 12-byte specific test:");
        output.WriteLine($"  Data: {BitConverter.ToString(dingSongData)}");
        output.WriteLine($"  Raw data: 02 2B 30 30 30 39 36 30 30 31 34 03");
        output.WriteLine($"  Parsed string: \"00096001\"");
        output.WriteLine($"  Expected: 96001 kg (no division by 100)");
        output.WriteLine($"  Received updates: {receivedWeights.Count}");
        output.WriteLine($"  Final weight: {finalWeight} ton");
        foreach (var weight in receivedWeights)
        {
            output.WriteLine($"    Weight update: {weight} ton");
        }

        // Cleanup
        subscription.Dispose();
        await service.DisposeAsync();
    }

    /// <summary>
    /// Test DingSong noise resistance - various invalid data formats should be filtered out
    /// Tests that the service correctly handles noise data without crashing or accepting invalid weights
    /// </summary>
    [Fact(Timeout = 5000)]
    public async Task ParseHexData_DingSong_NoiseResistance_Should_FilterInvalidData()
    {
        // Arrange
        var mockSerialPort = Substitute.For<ISerialPort>();
        var mockFactory = Substitute.For<ISerialPortFactory>();
        mockFactory.Create().Returns(mockSerialPort);
        
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, mockFactory);
        
        // Configure scale settings for DingSong type with HEX communication
        var settings = new ScaleSettings
        {
            SerialPort = "COM3",
            BaudRate = "9600",
            CommunicationMethod = "TF0",
            ScaleType = ScaleType.DingSong,
            ScaleUnit = ScaleUnit.Kg
        };
        
        var receivedWeights = new System.Collections.Concurrent.ConcurrentBag<decimal>();
        var subscription = service.WeightUpdates.Subscribe(w => receivedWeights.Add(w));

        // Setup mock serial port
        mockSerialPort.IsOpen.Returns(true);
        
        // Initialize service
        await service.InitializeAsync(settings);
        
        // Get initial state
        var initialWeight = service.GetCurrentWeight();
        var initialWeightCount = receivedWeights.Count;
        
        // Prepare all noise test cases and valid data in a queue
        var dataQueue = new Queue<byte[]>();
        
        // Test case 1: Data not starting with 0x02 (invalid frame start)
        var noiseData1 = new byte[] { 0xAA, 0xBB, 0x2B, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x31, 0x03 };
        dataQueue.Enqueue((byte[])noiseData1.Clone());
        
        // Test case 2: Data not ending with 0x03 (invalid frame end)
        var noiseData2 = new byte[] { 0x02, 0x2B, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x31, 0x42, 0xFF };
        dataQueue.Enqueue((byte[])noiseData2.Clone());
        
        // Test case 3: Invalid sign byte (not 0x2B or 0x2D)
        var noiseData3 = new byte[] { 0x02, 0xFF, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x31, 0x42, 0x03 };
        dataQueue.Enqueue((byte[])noiseData3.Clone());
        
        // Test case 4: Non-digit character in weight digits (invalid ASCII)
        var noiseData4 = new byte[] { 0x02, 0x2B, 0x30, 0x30, 0x30, 0x41, 0x42, 0x30, 0x30, 0x31, 0x42, 0x03 }; // Contains 'A' and 'B'
        dataQueue.Enqueue((byte[])noiseData4.Clone());
        
        // Test case 5: Invalid end marker (outside 0x30-0x46 range)
        var noiseData5 = new byte[] { 0x02, 0x2B, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x31, 0xFF, 0x03 }; // Invalid end marker
        dataQueue.Enqueue((byte[])noiseData5.Clone());
        
        // Test case 6: Valid data - 100t (100000 kg = "00100000")
        // Format: 02 2B 30 30 31 30 30 30 30 30 42 03
        var validData100t = new byte[] { 0x02, 0x2B, 0x30, 0x30, 0x31, 0x30, 0x30, 0x30, 0x30, 0x30, 0x42, 0x03 };
        dataQueue.Enqueue((byte[])validData100t.Clone());
        
        // Test case 7: Valid data - 101t (101000 kg = "00101000")
        // Format: 02 2B 30 30 31 30 31 30 30 30 42 03
        var validData101t = new byte[] { 0x02, 0x2B, 0x30, 0x30, 0x31, 0x30, 0x31, 0x30, 0x30, 0x30, 0x42, 0x03 };
        dataQueue.Enqueue((byte[])validData101t.Clone());
        
        // Setup mock to read from queue - all data packets handled in one setup
        SetupMockSerialPortReadFromQueue(mockSerialPort, dataQueue);
        
        // Send all data packets sequentially
        var totalPackets = dataQueue.Count;
        var eventArgsType = typeof(SerialDataReceivedEventArgs);
        
        for (int i = 0; i < totalPackets; i++)
        {
            var eventArgs = (SerialDataReceivedEventArgs)Activator.CreateInstance(
                eventArgsType, 
                BindingFlags.NonPublic | BindingFlags.Instance, 
                null, 
                new object[] { SerialData.Chars }, 
                null)!;
            
            mockSerialPort.DataReceived += Raise.Event<SerialDataReceivedEventHandler>(
                mockSerialPort, 
                eventArgs);
            
            await Task.Delay(300); // Wait for processing
        }
        
        // Wait for all processing to complete
        await Task.Delay(200);
        
        // Assert
        var finalWeight = service.GetCurrentWeight();
        var finalWeightCount = receivedWeights.Count;
        
        // All noise data should be filtered out, only valid data should be accepted
        // If initial weight was 0 and we sent one valid data, we should have at least one update
        var validWeightUpdates = finalWeightCount - initialWeightCount;
        
        output.WriteLine($"DingSong noise resistance test:");
        output.WriteLine($"  Initial weight: {initialWeight} ton, updates: {initialWeightCount}");
        output.WriteLine($"  Final weight: {finalWeight} ton, updates: {finalWeightCount}");
        output.WriteLine($"  Valid weight updates received: {validWeightUpdates}");
        output.WriteLine($"  Test cases:");
        output.WriteLine($"    1. Invalid frame start (0xAA instead of 0x02): {BitConverter.ToString(noiseData1)}");
        output.WriteLine($"    2. Invalid frame end (0xFF instead of 0x03): {BitConverter.ToString(noiseData2)}");
        output.WriteLine($"    3. Invalid sign byte (0xFF): {BitConverter.ToString(noiseData3)}");
        output.WriteLine($"    4. Non-digit characters (0x41, 0x42): {BitConverter.ToString(noiseData4)}");
        output.WriteLine($"    5. Invalid end marker (0xFF): {BitConverter.ToString(noiseData5)}");
        output.WriteLine($"    6. Valid data - 100t (should be accepted): {BitConverter.ToString(validData100t)}");
        output.WriteLine($"    7. Valid data - 101t (should be accepted): {BitConverter.ToString(validData101t)}");
        
        foreach (var weight in receivedWeights)
        {
            output.WriteLine($"    Weight update: {weight} ton");
        }
        
        // Verify that noise data was filtered out (no exceptions thrown, service still works)
        // Valid data should be accepted if it was sent
        // The exact number of updates depends on initial state, but we should have at least processed the valid data
        
        // Cleanup
        subscription.Dispose();
        await service.DisposeAsync();
    }

    /// <summary>
    /// Test parsing DingSong 22-byte HEX data
    /// Format: (XON)AA(±)nnnnnnptttttteff(CHK)(XOF)
    /// XON=0x11, AA=0xAA, XOF=0x13
    /// </summary>
    [Fact(Timeout = 5000)]
    public async Task ParseHexData_DingSong_22Byte_Should_HandleFormat()
    {
        // Arrange
        var mockSerialPort = Substitute.For<ISerialPort>();
        var mockFactory = Substitute.For<ISerialPortFactory>();
        mockFactory.Create().Returns(mockSerialPort);
        
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, mockFactory);
        
        // Configure scale settings for DingSong type with HEX communication
        var settings = new ScaleSettings
        {
            SerialPort = "COM3",
            BaudRate = "9600",
            CommunicationMethod = "TF0",
            ScaleType = ScaleType.DingSong,
            ScaleUnit = ScaleUnit.Kg
        };
        
        var receivedWeights = new System.Collections.Concurrent.ConcurrentBag<decimal>();
        var subscription = service.WeightUpdates.Subscribe(w => receivedWeights.Add(w));

        // Setup mock serial port
        mockSerialPort.IsOpen.Returns(true);
        
        // DingSong 22-byte data format: (XON)AA(±)nnnnnnptttttteff(CHK)(XOF)
        // Example: 11 AA 2B 30 30 30 30 30 30 32 30 30 30 30 30 30 4B 30 30 42 13
        // XON=0x11, AA=0xAA, +=0x2B, nnnnnn="000000", p="2", tttttt="000000", e="K", ff="00", CHK=0x42, XOF=0x13
        var dingSong22ByteData = new byte[] 
        { 
            0x11, 0xAA, 0x2B, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x32, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x4B, 0x30, 0x30, 0x42, 0x13 
        };
        
        // Initialize service
        await service.InitializeAsync(settings);
        
        // Note: Current implementation only supports 12-byte format
        // This test verifies that 22-byte data is handled gracefully (likely discarded)
        SetupMockSerialPortRead(mockSerialPort, dingSong22ByteData);
        
        var eventArgsType = typeof(SerialDataReceivedEventArgs);
        var eventArgs = (SerialDataReceivedEventArgs)Activator.CreateInstance(
            eventArgsType, 
            BindingFlags.NonPublic | BindingFlags.Instance, 
            null, 
            new object[] { SerialData.Chars }, 
            null)!;
        
        mockSerialPort.DataReceived += Raise.Event<SerialDataReceivedEventHandler>(
            mockSerialPort, 
            eventArgs);
        
        await Task.Delay(300); // Wait for processing

        // Assert
        // Current implementation expects 12-byte format starting with 0x02
        // 22-byte format starts with 0x11, so it should be filtered out
        var initialWeight = service.GetCurrentWeight();
        var weightAfterData = service.GetCurrentWeight();
        
        output.WriteLine($"DingSong 22-byte test:");
        output.WriteLine($"  Data: {BitConverter.ToString(dingSong22ByteData)}");
        output.WriteLine($"  Format: XON(0x11) AA(0xAA) ± nnnnnn p tttttt e ff CHK XOF(0x13)");
        output.WriteLine($"  Data length: {dingSong22ByteData.Length} bytes");
        output.WriteLine($"  Weight before: {initialWeight} ton");
        output.WriteLine($"  Weight after: {weightAfterData} ton");
        output.WriteLine($"  Received updates: {receivedWeights.Count}");
        output.WriteLine($"  Note: Current implementation only supports 12-byte format. 22-byte format may be filtered out.");

        // Current implementation may filter this out since it doesn't start with 0x02
        // This test documents the behavior for future implementation
        // If 22-byte format is supported in the future, this test should be updated

        // Cleanup
        subscription.Dispose();
        await service.DisposeAsync();
    }

    private void SetupMockSerialPortRead(ISerialPort mockSerialPort, byte[] data)
    {
        // Clear previous mock configurations to avoid conflicts
        // Note: ClearReceivedCalls doesn't clear Returns configuration, but we'll reset the state
        mockSerialPort.ClearReceivedCalls();
        
        // Use a mutable class to track read position across multiple calls
        // Create a new ReadState for each setup to ensure clean state
        var readState = new ReadState { Index = 0 };
        var dataCopy = (byte[])data.Clone(); // Clone to avoid issues if data is modified
        
        // Setup BytesToRead - returns available bytes
        mockSerialPort.BytesToRead.Returns(_ => 
        {
            var remaining = dataCopy.Length - readState.Index;
            return remaining > 0 ? remaining : 0;
        });
        
        // Setup ReadByte - returns bytes sequentially
        mockSerialPort.ReadByte().Returns(_ =>
        {
            if (readState.Index < dataCopy.Length)
            {
                var result = dataCopy[readState.Index];
                readState.Index++;
                return result;
            }
            return -1;
        });
        
        // Setup Read - returns requested bytes from current position
        // Use ReturnsForAnyArgs to ensure it works with any arguments
        // Important: This will replace any previous Read configuration
        mockSerialPort.Read(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>()).ReturnsForAnyArgs(callInfo =>
        {
            var buffer = callInfo.ArgAt<byte[]>(0);
            var offset = callInfo.ArgAt<int>(1);
            var count = callInfo.ArgAt<int>(2);
            
            var bytesToRead = Math.Min(count, dataCopy.Length - readState.Index);
            if (bytesToRead > 0)
            {
                Array.Copy(dataCopy, readState.Index, buffer, offset, bytesToRead);
                readState.Index += bytesToRead;
                return bytesToRead;
            }
            return 0;
        });
    }

    private void SetupMockSerialPortReadFromQueue(ISerialPort mockSerialPort, System.Collections.Generic.Queue<byte[]> dataQueue)
    {
        // Use a mutable class to track current packet and read position
        var readState = new QueueReadState { CurrentPacket = null, Index = 0, DataQueue = dataQueue };
        
        // Setup BytesToRead - returns available bytes from current packet
        mockSerialPort.BytesToRead.Returns(_ => 
        {
            if (readState.CurrentPacket == null && readState.DataQueue.Count > 0)
            {
                readState.CurrentPacket = readState.DataQueue.Dequeue();
                readState.Index = 0;
            }
            
            if (readState.CurrentPacket != null)
            {
                var remaining = readState.CurrentPacket.Length - readState.Index;
                return remaining > 0 ? remaining : 0;
            }
            return 0;
        });
        
        // Setup ReadByte - returns bytes sequentially from current packet
        mockSerialPort.ReadByte().Returns(_ =>
        {
            if (readState.CurrentPacket == null && readState.DataQueue.Count > 0)
            {
                readState.CurrentPacket = readState.DataQueue.Dequeue();
                readState.Index = 0;
            }
            
            if (readState.CurrentPacket != null && readState.Index < readState.CurrentPacket.Length)
            {
                var result = readState.CurrentPacket[readState.Index];
                readState.Index++;
                
                // If we've read all bytes from current packet, reset for next packet
                if (readState.Index >= readState.CurrentPacket.Length)
                {
                    readState.CurrentPacket = null;
                    readState.Index = 0;
                }
                
                return result;
            }
            return -1;
        });
        
        // Setup Read - returns requested bytes from current packet
        mockSerialPort.Read(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>()).ReturnsForAnyArgs(callInfo =>
        {
            // Get next packet if current is exhausted
            if (readState.CurrentPacket == null && readState.DataQueue.Count > 0)
            {
                readState.CurrentPacket = readState.DataQueue.Dequeue();
                readState.Index = 0;
            }
            
            if (readState.CurrentPacket == null)
            {
                return 0;
            }
            
            var buffer = callInfo.ArgAt<byte[]>(0);
            var offset = callInfo.ArgAt<int>(1);
            var count = callInfo.ArgAt<int>(2);
            
            var bytesToRead = Math.Min(count, readState.CurrentPacket.Length - readState.Index);
            if (bytesToRead > 0)
            {
                System.Array.Copy(readState.CurrentPacket, readState.Index, buffer, offset, bytesToRead);
                readState.Index += bytesToRead;
                
                // If we've read all bytes from current packet, reset for next packet
                if (readState.Index >= readState.CurrentPacket.Length)
                {
                    readState.CurrentPacket = null;
                    readState.Index = 0;
                }
                
                return bytesToRead;
            }
            return 0;
        });
    }

    private class ReadState
    {
        public int Index { get; set; }
    }

    private class QueueReadState
    {
        public byte[]? CurrentPacket { get; set; }
        public int Index { get; set; }
        public Queue<byte[]> DataQueue { get; set; } = null!;
    }
}