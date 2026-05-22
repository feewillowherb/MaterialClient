using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Shouldly;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     Unit tests for SoundDeviceService
///     Tests soundDeviceSettings and text parameters with actual HTTP calls
/// </summary>
public class SoundDeviceServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly List<IDisposable> _disposables = new();

    public SoundDeviceServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    ///     Test PlayTextAsync with valid soundDeviceSettings and text
    /// </summary>
    [Fact(Skip = "待移植至集成测试项目: 依赖真实 HTTP 调用")]
    public async Task PlayTextAsync_Should_PlayText_WithValidSettings()
    {
        // Arrange
        var soundDeviceSettings = new SoundDeviceSettings
        {
            Enabled = true,
            LocalIP = "192.168.4.56",
            SoundIP = "localhost",
            SoundSN = "test_sn_001",
            SoundVolume = "80"
        };

        var text = "测试文本：这是一条测试消息";

        var service = CreateSoundDeviceService(soundDeviceSettings);

        // Act
        await service.PlayTextAsync(text, CancellationToken.None);

        // Assert - Should complete without throwing
        // Note: Actual HTTP call may fail if device is not available, but service should handle it gracefully
    }

    /// <summary>
    ///     Test PlayTextAsync with different volume settings
    /// </summary>
    [Theory(Skip = "待移植至集成测试项目: 依赖真实 HTTP 调用")]
    [InlineData("0")] // "0" means 100
    [InlineData("50")]
    [InlineData("100")]
    [InlineData("75")]
    public async Task PlayTextAsync_Should_UseCorrectVolume(string volumeSetting)
    {
        // Arrange
        var soundDeviceSettings = new SoundDeviceSettings
        {
            Enabled = true,
            LocalIP = "localhost",
            SoundIP = "localhost",
            SoundSN = "test_sn_002",
            SoundVolume = volumeSetting
        };

        var text = "音量测试";

        var service = CreateSoundDeviceService(soundDeviceSettings);

        // Act
        await service.PlayTextAsync(text, CancellationToken.None);

        // Assert - Should complete without throwing
    }

    /// <summary>
    ///     Test PlayTextAsync with different text content
    /// </summary>
    [Theory(Skip = "待移植至集成测试项目: 依赖真实 HTTP 调用")]
    [InlineData("简单文本")]
    [InlineData("包含特殊字符的文本：!@#$%^&*()")]
    [InlineData("包含URL的文本：http://example.com/test")]
    [InlineData("包含空格的文本 测试")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PlayTextAsync_Should_HandleDifferentText(string text)
    {
        // Arrange
        var soundDeviceSettings = new SoundDeviceSettings
        {
            Enabled = true,
            LocalIP = "localhost",
            SoundIP = "localhost",
            SoundSN = "test_sn_003",
            SoundVolume = "50"
        };

        var service = CreateSoundDeviceService(soundDeviceSettings);

        // Act
        await service.PlayTextAsync(text, CancellationToken.None);

        // Assert - Should complete without throwing
        // Empty or whitespace text should be handled gracefully
    }

    /// <summary>
    ///     Test PlayTextAsync when sound device is disabled
    /// </summary>
    [Fact(Skip = "待移植至集成测试项目: 依赖真实 HTTP 调用")]
    public async Task PlayTextAsync_Should_Skip_WhenDisabled()
    {
        // Arrange
        var soundDeviceSettings = new SoundDeviceSettings
        {
            Enabled = false,
            LocalIP = "localhost",
            SoundIP = "localhost",
            SoundSN = "test_sn_004",
            SoundVolume = "50"
        };

        var text = "这条消息不应该播放";

        var service = CreateSoundDeviceService(soundDeviceSettings);

        // Act
        await service.PlayTextAsync(text, CancellationToken.None);

        // Assert - Should complete without throwing and without making HTTP calls
    }

    /// <summary>
    ///     Test PlayTextAsync with invalid settings (missing IP or SN)
    /// </summary>
    [Theory(Skip = "待移植至集成测试项目: 依赖真实 HTTP 调用")]
    [InlineData("", "localhost", "test_sn")] // Empty LocalIP
    [InlineData("localhost", "", "test_sn")] // Empty SoundIP
    [InlineData("localhost", "localhost", "")] // Empty SoundSN
    public async Task PlayTextAsync_Should_Skip_WithInvalidSettings(
        string localIP, string soundIP, string soundSN)
    {
        // Arrange
        var soundDeviceSettings = new SoundDeviceSettings
        {
            Enabled = true,
            LocalIP = localIP,
            SoundIP = soundIP,
            SoundSN = soundSN,
            SoundVolume = "50"
        };

        var text = "这条消息不应该播放，因为配置无效";

        var service = CreateSoundDeviceService(soundDeviceSettings);

        // Act
        await service.PlayTextAsync(text, CancellationToken.None);

        // Assert - Should complete without throwing and without making HTTP calls
    }

    /// <summary>
    ///     Test PlayTextAsync with different IP addresses
    /// </summary>
    [Theory(Skip = "待移植至集成测试项目: 依赖真实 HTTP 调用")]
    [InlineData("127.0.0.1", "127.0.0.1")]
    [InlineData("192.168.1.100", "192.168.1.101")]
    [InlineData("10.0.0.1", "10.0.0.2")]
    public async Task PlayTextAsync_Should_UseCorrectIPs(string localIP, string soundIP)
    {
        // Arrange
        var soundDeviceSettings = new SoundDeviceSettings
        {
            Enabled = true,
            LocalIP = localIP,
            SoundIP = soundIP,
            SoundSN = "test_sn_005",
            SoundVolume = "60"
        };

        var text = "IP地址测试";

        var service = CreateSoundDeviceService(soundDeviceSettings);

        // Act
        await service.PlayTextAsync(text, CancellationToken.None);

        // Assert - Should complete without throwing
    }

    /// <summary>
    ///     Test PlayTextAsync with long text
    /// </summary>
    [Fact(Skip = "待移植至集成测试项目: 依赖真实 HTTP 调用")]
    public async Task PlayTextAsync_Should_HandleLongText()
    {
        // Arrange
        var soundDeviceSettings = new SoundDeviceSettings
        {
            Enabled = true,
            LocalIP = "localhost",
            SoundIP = "localhost",
            SoundSN = "test_sn_006",
            SoundVolume = "70"
        };

        var longText = string.Join(" ", Enumerable.Range(1, 100).Select(i => $"单词{i}"));

        var service = CreateSoundDeviceService(soundDeviceSettings);

        // Act
        await service.PlayTextAsync(longText, CancellationToken.None);

        // Assert - Should complete without throwing
    }

    /// <summary>
    ///     Test PlayTextAsync with special characters in text
    /// </summary>
    [Fact(Skip = "待移植至集成测试项目: 依赖真实 HTTP 调用")]
    public async Task PlayTextAsync_Should_EscapeSpecialCharacters()
    {
        // Arrange
        var soundDeviceSettings = new SoundDeviceSettings
        {
            Enabled = true,
            LocalIP = "localhost",
            SoundIP = "localhost",
            SoundSN = "test_sn_007",
            SoundVolume = "65"
        };

        var textWithSpecialChars = "测试 & 特殊字符 < > \" ' ? = +";

        var service = CreateSoundDeviceService(soundDeviceSettings);

        // Act
        await service.PlayTextAsync(textWithSpecialChars, CancellationToken.None);

        // Assert - Should complete without throwing
    }

    /// <summary>
    ///     Create SoundDeviceService with specified soundDeviceSettings
    /// </summary>
    private ISoundDeviceService CreateSoundDeviceService(SoundDeviceSettings soundDeviceSettings)
    {
        // Create a real ISettingsService implementation (not a mock)
        var settingsService = new TestSettingsService(soundDeviceSettings);

        // Create a real IHttpClientFactory
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddHttpClient();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        _disposables.Add(serviceProvider);

        // Create logger
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<SoundDeviceService>();
        _disposables.Add(loggerFactory);

        // Create service using AutoConstructor (manual construction)
        var service = new SoundDeviceService(httpClientFactory, logger, settingsService);
        return service;
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
        _disposables.Clear();
    }

    /// <summary>
    ///     Test implementation of ISettingsService that returns configured settings
    ///     This is not a mock - it's a real implementation for testing
    /// </summary>
    private class TestSettingsService : ISettingsService
    {
        private readonly SoundDeviceSettings _soundDeviceSettings;

        public TestSettingsService(SoundDeviceSettings soundDeviceSettings)
        {
            _soundDeviceSettings = soundDeviceSettings;
        }

        public Task<SettingsEntity> GetSettingsAsync()
        {
            var settings = new SettingsEntity(
                scaleSettings: new ScaleSettings(),
                documentScannerConfig: new DocumentScannerConfig(),
                systemSettings: new SystemSettings(),
                cameraConfigs: new List<CameraConfig>(),
                licensePlateRecognitionConfigs: new List<LicensePlateRecognitionConfig>(),
                weighingConfiguration: new WeighingConfiguration(),
                soundDeviceSettings: _soundDeviceSettings
            );

            return Task.FromResult(settings);
        }

        public Task SaveSettingsAsync(SettingsEntity settings)
        {
            // Not used in tests
            return Task.CompletedTask;
        }

        public async Task<WeighingMode> GetWeighingModeAsync()
        {
            var settings = await GetSettingsAsync();
            return settings.SystemSettings.DefaultWeighingMode;
        }

        public async Task<ProductCode> GetProductCodeAsync()
        {
            var weighingMode = await GetWeighingModeAsync();
            return weighingMode switch
            {
                WeighingMode.SolidWaste => ProductCode.SolidWaste,
                WeighingMode.UrbanMode => ProductCode.Urban,
                _ => ProductCode.Standard
            };
        }

        public Task SaveDefaultWeighingModeAsync(ProductCode productCode) => Task.CompletedTask;
    }
}

