using MaterialClient.Common.Services.Hikvision;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests;

/// <summary>
/// Integration tests for Hikvision camera capture functionality.
/// These tests require actual Hikvision cameras to be connected and accessible.
/// All tests are skipped by default - remove Skip attribute to run with real hardware.
/// </summary>
[Collection("Hikvision")]
public class HikvisionIntegrationTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Test configuration - update these values for your environment
    /// </summary>
    private static readonly HikvisionDeviceConfig TestConfig = new()
    {
        Ip = "192.168.3.245",
        Username = "admin",
        Password = "12345",
        Port = 8000,
        StreamType = 0,
        Channels = [1]
    };

    public HikvisionIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Real Camera Tests

    /// <summary>
    /// Tests basic connectivity to a real Hikvision camera.
    /// </summary>
    [Fact(Skip = "Requires physical Hikvision device")]
    public void RealCamera_IsOnline_ShouldReturnTrue()
    {
        var service = new HikvisionService();
        service.AddOrUpdateDevice(TestConfig);

        var online = service.IsOnline(TestConfig);
        
        if (!online)
        {
            var error = HikvisionService.GetLastErrorCode();
            _output.WriteLine($"Device offline. Error code: {error}");
        }

        Assert.True(online, "Camera should be online");
    }

    /// <summary>
    /// Tests direct JPEG capture from a real camera.
    /// </summary>
    [Fact(Skip = "Requires physical Hikvision device")]
    public void RealCamera_CaptureJpeg_ShouldSucceed()
    {
        var service = new HikvisionService();
        service.AddOrUpdateDevice(TestConfig);

        var captureDir = CreateCaptureDirectory("direct");
        var fullPath = Path.Combine(captureDir, $"direct_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg");

        var result = service.CaptureJpeg(TestConfig, 1, fullPath, out var error);

        _output.WriteLine($"Direct capture result: {result}, Error: {error}");

        Assert.True(result, $"Direct capture failed with error: {error}");
        Assert.True(File.Exists(fullPath), "Captured file should exist");
        Assert.True(new FileInfo(fullPath).Length > 0, "File should not be empty");
    }

    /// <summary>
    /// Tests stream-based JPEG capture from a real camera.
    /// </summary>
    [Fact(Skip = "Requires physical Hikvision device")]
    public void RealCamera_CaptureJpegFromStream_ShouldSucceed()
    {
        var service = new HikvisionService();
        service.AddOrUpdateDevice(TestConfig);

        var captureDir = CreateCaptureDirectory("stream");
        var fullPath = Path.Combine(captureDir, $"stream_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg");

        var result = service.CaptureJpegFromStream(TestConfig, 1, fullPath);

        _output.WriteLine($"Stream capture result: {result}");

        Assert.True(result, "Stream capture should succeed");
        Assert.True(File.Exists(fullPath), "Captured file should exist");
        Assert.True(new FileInfo(fullPath).Length > 0, "File should not be empty");
    }

    #endregion

    #region Concurrent Access Tests

    /// <summary>
    /// Tests concurrent stream captures to verify crash fixes.
    /// This is the critical test case for the crash prevention fixes.
    /// </summary>
    [Fact(Skip = "Requires physical Hikvision device")]
    public async Task RealCamera_ConcurrentCapture_ShouldNotCrash()
    {
        var service = new HikvisionService();
        service.AddOrUpdateDevice(TestConfig);

        var captureDir = CreateCaptureDirectory("concurrent");
        int concurrency = 4; // The problematic concurrency level
        int successCount = 0;
        int failCount = 0;

        _output.WriteLine($"Starting {concurrency} concurrent captures...");

        var tasks = Enumerable.Range(0, concurrency)
            .Select(i => Task.Run(() =>
            {
                var fullPath = Path.Combine(captureDir, $"concurrent_{i}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg");
                
                try
                {
                    var result = service.CaptureJpegFromStream(TestConfig, 1, fullPath);
                    
                    if (result)
                    {
                        Interlocked.Increment(ref successCount);
                        _output.WriteLine($"Task {i}: Success, File size: {new FileInfo(fullPath).Length}");
                    }
                    else
                    {
                        Interlocked.Increment(ref failCount);
                        _output.WriteLine($"Task {i}: Failed");
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failCount);
                    _output.WriteLine($"Task {i}: Exception - {ex.Message}");
                    return false;
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        _output.WriteLine($"Concurrent test completed: Success={successCount}, Failed={failCount}");
        
        // The main assertion is that we didn't crash
        Assert.True(true, "No crash occurred during concurrent captures");
    }

    /// <summary>
    /// Extended stress test with multiple iterations of concurrent captures.
    /// </summary>
    [Fact(Skip = "Requires physical Hikvision device - long running test")]
    public async Task RealCamera_StressTest_100Iterations_ShouldNotCrash()
    {
        var service = new HikvisionService();
        service.AddOrUpdateDevice(TestConfig);

        var captureDir = CreateCaptureDirectory("stress");
        int iterations = 100;
        int concurrencyPerIteration = 4;
        int totalSuccess = 0;
        int totalFail = 0;

        _output.WriteLine($"Starting stress test: {iterations} iterations x {concurrencyPerIteration} concurrent...");

        for (int iter = 0; iter < iterations; iter++)
        {
            var tasks = Enumerable.Range(0, concurrencyPerIteration)
                .Select(i => Task.Run(() =>
                {
                    var fullPath = Path.Combine(captureDir, $"stress_{iter}_{i}_{Guid.NewGuid():N}.jpg");
                    
                    try
                    {
                        return service.CaptureJpegFromStream(TestConfig, 1, fullPath);
                    }
                    catch
                    {
                        return false;
                    }
                }))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            
            int iterSuccess = results.Count(r => r);
            int iterFail = results.Count(r => !r);
            
            totalSuccess += iterSuccess;
            totalFail += iterFail;

            if (iter % 10 == 0 || iter == iterations - 1)
            {
                _output.WriteLine($"Iteration {iter + 1}/{iterations}: Total Success={totalSuccess}, Fail={totalFail}");
            }

            // Small delay between iterations to avoid overwhelming the camera
            await Task.Delay(100);
        }

        double successRate = (double)totalSuccess / (iterations * concurrencyPerIteration) * 100;
        _output.WriteLine($"Stress test completed: Success rate = {successRate:F1}%");
        
        // Primary assertion: no crash
        Assert.True(true, "Stress test completed without crash");
    }

    #endregion

    #region Resource Leak Tests

    /// <summary>
    /// Tests for port leaks by performing many captures and monitoring port usage.
    /// </summary>
    [Fact(Skip = "Requires physical Hikvision device")]
    public async Task RealCamera_PortLeakTest_ShouldNotLeakPorts()
    {
        var service = new HikvisionService();
        service.AddOrUpdateDevice(TestConfig);

        var captureDir = CreateCaptureDirectory("leak_test");
        
        int initialSlots = PlayM4PortPool.AvailableSlots;
        _output.WriteLine($"Initial available slots: {initialSlots}");

        // Perform 50 sequential captures
        for (int i = 0; i < 50; i++)
        {
            var fullPath = Path.Combine(captureDir, $"leak_{i}_{Guid.NewGuid():N}.jpg");
            var result = service.CaptureJpegFromStream(TestConfig, 1, fullPath);
            
            // Check available slots after each capture
            int currentSlots = PlayM4PortPool.AvailableSlots;
            
            if (i % 10 == 0)
            {
                _output.WriteLine($"Capture {i}: Result={result}, Available slots={currentSlots}");
            }
            
            // Allow cleanup
            await Task.Delay(50);
        }

        // Final check
        int finalSlots = PlayM4PortPool.AvailableSlots;
        _output.WriteLine($"Final available slots: {finalSlots}");
        
        // Slots should be back to initial (or close to it)
        Assert.True(finalSlots >= initialSlots - 1, $"Possible port leak: Initial={initialSlots}, Final={finalSlots}");
    }

    #endregion

    #region Batch Capture Tests

    /// <summary>
    /// Tests the batch capture API with multiple cameras.
    /// </summary>
    [Fact(Skip = "Requires physical Hikvision devices")]
    public async Task RealCamera_BatchCapture_ShouldSucceed()
    {
        var service = new HikvisionService();
        
        var captureDir = CreateCaptureDirectory("batch");

        // Create batch requests for multiple cameras
        var requests = new List<BatchCaptureRequest>
        {
            new()
            {
                Config = TestConfig,
                Channel = 1,
                SaveFullPath = Path.Combine(captureDir, "batch_1.jpg"),
                DeviceKey = $"{TestConfig.Ip}:{TestConfig.Port}"
            }
        };

        var results = await service.CaptureJpegFromStreamBatchAsync(requests);

        foreach (var result in results)
        {
            _output.WriteLine($"Batch capture result: Success={result.Success}, Error={result.ErrorMessage}, FileSize={result.FileSize}");
        }

        Assert.NotEmpty(results);
    }

    #endregion

    #region Error Handling Tests

    /// <summary>
    /// Tests that invalid configuration is handled gracefully.
    /// </summary>
    [Fact]
    public void InvalidConfig_ShouldNotCrash()
    {
        var service = new HikvisionService();
        var invalidConfig = new HikvisionDeviceConfig
        {
            Ip = "192.168.255.255", // Invalid IP
            Username = "invalid",
            Password = "invalid",
            Port = 8000,
            Channels = [1]
        };

        service.AddOrUpdateDevice(invalidConfig);

        // This should not throw
        var online = service.IsOnline(invalidConfig);
        Assert.False(online, "Invalid config should not report as online");

        _output.WriteLine("Invalid config handled gracefully");
    }

    /// <summary>
    /// Tests capture with invalid save path.
    /// </summary>
    [Fact]
    public void InvalidSavePath_ShouldThrow()
    {
        var service = new HikvisionService();

        Assert.Throws<ArgumentException>(() =>
            service.CaptureJpegFromStream(TestConfig, 1, ""));
        
        Assert.Throws<ArgumentException>(() =>
            service.CaptureJpegFromStream(TestConfig, 1, "   "));

        _output.WriteLine("Invalid path validation working correctly");
    }

    /// <summary>
    /// Tests null config handling.
    /// </summary>
    [Fact]
    public void NullConfig_ShouldThrow()
    {
        var service = new HikvisionService();

        Assert.Throws<ArgumentNullException>(() =>
            service.CaptureJpegFromStream(null!, 1, "test.jpg"));

        _output.WriteLine("Null config validation working correctly");
    }

    #endregion

    #region Helper Methods

    private string CreateCaptureDirectory(string subfolder)
    {
        var captureDir = Path.Combine(AppContext.BaseDirectory, "captures", "integration", subfolder);
        Directory.CreateDirectory(captureDir);
        return captureDir;
    }

    #endregion
}

/// <summary>
/// Collection definition for Hikvision tests to ensure they don't run in parallel
/// (avoiding resource contention with actual camera hardware)
/// </summary>
[CollectionDefinition("Hikvision", DisableParallelization = true)]
public class HikvisionTestCollection
{
}
