using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Services;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class UrbanAnomalyDetectionConfigLoaderTests
{
    [Fact]
    public async Task LoadAsync_ShouldPreferSettingsOverAppsettings()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.GetSettingsAsync().Returns(new SettingsEntity(
            new ScaleSettings(),
            new DocumentScannerConfig(),
            new SystemSettings
            {
                UrbanAnomalyDetection = new UrbanAnomalyDetectionConfig
                {
                    UpperLimit = 42m,
                    LowerLimit = 3m,
                    DeviationPercentage = 15m
                }
            },
            [],
            [],
            new WeighingConfiguration(),
            new SoundDeviceSettings()));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UrbanAnomalyDetection:UpperLimit"] = "99",
                ["UrbanAnomalyDetection:LowerLimit"] = "1",
                ["UrbanAnomalyDetection:DeviationPercentage"] = "10"
            })
            .Build();

        var config = await UrbanAnomalyDetectionConfigLoader.LoadAsync(settingsService, configuration);

        Assert.Equal(42m, config.UpperLimit);
        Assert.Equal(3m, config.LowerLimit);
        Assert.Equal(15m, config.DeviationPercentage);
    }

    [Fact]
    public async Task LoadAsync_ShouldFallbackToAppsettingsWhenSettingsUnavailable()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.GetSettingsAsync().Returns<Task<SettingsEntity>>(_ => throw new InvalidOperationException("db down"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UrbanAnomalyDetection:UpperLimit"] = "55",
                ["UrbanAnomalyDetection:LowerLimit"] = "2.5",
                ["UrbanAnomalyDetection:DeviationPercentage"] = "12"
            })
            .Build();

        var config = await UrbanAnomalyDetectionConfigLoader.LoadAsync(settingsService, configuration);

        Assert.Equal(55m, config.UpperLimit);
        Assert.Equal(2.5m, config.LowerLimit);
        Assert.Equal(12m, config.DeviationPercentage);
    }
}
