using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace MaterialClient.Common.Tests;

/// <summary>
/// Examples showing how to override configuration for different test scenarios
/// 推荐方式：在 Scenario 初始化时通过 Replace IOptions<XXX> 方式配置
/// </summary>

#region Example 1: Using Default Configuration from TestBase

public class UsingDefaultConfigurationTests : MaterialClientTestBase<MaterialClientDomainTestModule>
{
    [Fact]
    public void Should_Use_Default_In_Memory_Database()
    {
        // Configuration is set in MaterialClientTestBase.BeforeAddApplication()
        // Default: "ConnectionStrings:Default" = "Data Source=:memory:"

        var configuration = ServiceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("Default");

        connectionString.ShouldBe("Data Source=:memory:");
    }
}

#endregion

#region Example 2: Replace IOptions in Scenario Initialization (Recommended)

/// <summary>
/// 推荐方式：在测试方法中直接替换 IOptions<T>
/// 适用于单个测试场景需要特定配置的情况
/// </summary>
public class ReplaceIOptionsTests : MaterialClientTestBase<MaterialClientDomainTestModule>
{
    [Fact]
    public void Should_Test_With_Custom_WeighingConfiguration()
    {
        // Scenario: Test with custom weighing configuration
        // 在测试场景初始化时替换配置
        var customConfig = new WeighingConfiguration
        {
            MinWeightThreshold = 1.0m,  // 提高最小重量阈值
            WeightStabilityThreshold = 0.1m,
            StabilityWindowMs = 5000,    // 更长的稳定窗口
            StabilityCheckIntervalMs = 500,
            MaxIntervalMinutes = 600,
            MinWeightDiff = 2.0m
        };

        // Replace IOptions<WeighingConfiguration> in the service provider
        var options = Options.Create(customConfig);
        ServiceProvider.GetRequiredService<IServiceCollection>()
            .Replace(ServiceDescriptor.Singleton(typeof(IOptions<WeighingConfiguration>), options));

        // 或者直接使用配置对象进行测试
       // var service = new YourServiceUnderTest(options);
        // Act & Assert...
    }

    [Fact]
    public void Should_Test_With_Different_SystemSettings()
    {
        // Scenario: Test with different system settings
        var customSettings = new SystemSettings
        {
            EnableAutoStart = true,
            CaptureStreamType = StreamType.Mainstream,  // 使用主码流
            Urls = "http://test-server:8080",
            LprDeviceType = LprDeviceType.LprAllInOne,
            MinDiffCharCount = 1
        };

        var options = Options.Create(customSettings);

        // 在测试中使用自定义配置
        var lprDeviceType = customSettings.LprDeviceType;
        lprDeviceType.ShouldBe(LprDeviceType.LprAllInOne);
    }

    [Fact]
    public void Should_Test_With_Strict_Weight_Stability()
    {
        // Scenario: Test with strict weight stability settings
        var strictConfig = new WeighingConfiguration
        {
            MinWeightThreshold = 0.5m,
            WeightStabilityThreshold = 0.01m,  // 更严格的稳定性阈值
            StabilityWindowMs = 5000,          // 更长的监控窗口
            StabilityCheckIntervalMs = 100,    // 更频繁的检查
            MaxIntervalMinutes = 300,
            MinWeightDiff = 1m
        };

        strictConfig.IsValid().ShouldBeTrue();
        strictConfig.WeightStabilityThreshold.ShouldBe(0.01m);
    }
}

#endregion

#region Example 3: Test Module with Configuration Replacement

/// <summary>
/// 在 Module 级别替换配置
/// 适用于一组测试共享相同配置的场景
/// </summary>
[DependsOn(typeof(MaterialClientCommonModule))]
public class CustomWeighingConfigTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Scenario: 测试场景使用自定义称重配置
        var customWeighingConfig = new WeighingConfiguration
        {
            MinWeightThreshold = 0.8m,
            WeightStabilityThreshold = 0.03m,
            StabilityWindowMs = 4000,
            StabilityCheckIntervalMs = 300,
            MaxIntervalMinutes = 240,
            MinWeightDiff = 1.5m
        };

        // 替换 IOptions<WeighingConfiguration>
        context.Services.Replace(
            ServiceDescriptor.Singleton(
                typeof(IOptions<WeighingConfiguration>),
                Options.Create(customWeighingConfig)
            )
        );
    }
}

public class CustomWeighingConfigTests : MaterialClientTestBase<CustomWeighingConfigTestModule>
{
    [Fact]
    public void Should_Use_Custom_Weighing_Configuration()
    {
        var options = ServiceProvider.GetRequiredService<IOptions<WeighingConfiguration>>();
        var config = options.Value;

        config.MinWeightThreshold.ShouldBe(0.8m);
        config.WeightStabilityThreshold.ShouldBe(0.03m);
        config.StabilityWindowMs.ShouldBe(4000);
    }
}

#endregion

#region Example 4: Multiple Configuration Replacements in One Scenario

/// <summary>
/// 在一个测试场景中替换多个配置
/// </summary>
public class MultipleConfigReplacementsTests : MaterialClientTestBase<MaterialClientDomainTestModule>
{
    [Fact]
    public void Should_Test_With_Multiple_Custom_Configurations()
    {
        // Scenario 1: 自定义称重配置
        var weighingConfig = new WeighingConfiguration
        {
            MinWeightThreshold = 0.6m,
            WeightStabilityThreshold = 0.04m,
            StabilityWindowMs = 3500,
            StabilityCheckIntervalMs = 250,
            MaxIntervalMinutes = 280,
            MinWeightDiff = 1.2m
        };

        // Scenario 2: 自定义系统设置
        var systemSettings = new SystemSettings
        {
            EnableAutoStart = false,
            CaptureStreamType = StreamType.Substream,
            Urls = "http://integration-test.local:9999",
            LprDeviceType = LprDeviceType.Hikvision,
            MinDiffCharCount = 2
        };

        // 创建多个配置选项
        var weighingOptions = Options.Create(weighingConfig);
        var systemSettingsOptions = Options.Create(systemSettings);

        // 在测试中使用这些配置
        weighingConfig.IsValid().ShouldBeTrue();
        systemSettings.MinDiffCharCount.ShouldBe(2);

        // 可以将这些配置传递给服务进行测试
        // var service = new YourService(weighingOptions, systemSettingsOptions);
    }
}

#endregion

#region Example 5: Configuration Testing Best Practices

/// <summary>
/// 配置测试最佳实践示例
/// </summary>
public class ConfigurationBestPracticesTests : MaterialClientTestBase<MaterialClientDomainTestModule>
{
    [Fact]
    public void Should_Validate_Configuration_Before_Test()
    {
        // Best Practice: 在测试前验证配置有效性
        var config = new WeighingConfiguration
        {
            MinWeightThreshold = 0.5m,
            WeightStabilityThreshold = 0.05m,
            StabilityWindowMs = 3000,
            StabilityCheckIntervalMs = 200,
            MaxIntervalMinutes = 300,
            MinWeightDiff = 1m
        };

        // 验证配置
        config.IsValid().ShouldBeTrue();

        // 确保配置符合测试场景要求
        config.MinWeightThreshold.ShouldBeGreaterThan(0);
        config.StabilityCheckIntervalMs.ShouldBeLessThanOrEqualTo(config.StabilityWindowMs);
    }

    [Fact]
    public void Should_Test_Edge_Case_Configurations()
    {
        // Best Practice: 测试边界条件配置
        var edgeCaseConfig = new WeighingConfiguration
        {
            MinWeightThreshold = decimal.MinValue,  // 无效配置
            WeightStabilityThreshold = 0.05m,
            StabilityWindowMs = 3000,
            StabilityCheckIntervalMs = 200,
            MaxIntervalMinutes = 300,
            MinWeightDiff = 1m
        };

        // 应该被验证为无效
        edgeCaseConfig.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void Should_Isolate_Configuration_Between_Tests()
    {
        // Best Practice: 确保测试之间的配置隔离
        var test1Config = new WeighingConfiguration
        {
            MinWeightThreshold = 0.5m,
            WeightStabilityThreshold = 0.05m
        };

        var test2Config = new WeighingConfiguration
        {
            MinWeightThreshold = 1.0m,  // 不同的值
            WeightStabilityThreshold = 0.1m
        };

        // 每个测试使用独立的配置对象
        test1Config.MinWeightThreshold.ShouldNotBe(test2Config.MinWeightThreshold);
        test1Config.WeightStabilityThreshold.ShouldNotBe(test2Config.WeightStabilityThreshold);
    }
}

#endregion

#region Example 6: Unit Tests Without TestBase (Direct Configuration)

/// <summary>
/// 不依赖 TestBase 的单元测试示例
/// 适用于纯粹的业务逻辑测试，不需要完整的 ABP 环境
/// </summary>
public class DirectConfigurationUnitTests
{
    [Fact]
    public void Should_Test_WeighingConfiguration_Validation()
    {
        // Arrange
        var config = new WeighingConfiguration
        {
            MinWeightThreshold = 0.5m,
            WeightStabilityThreshold = 0.05m,
            StabilityWindowMs = 3000,
            StabilityCheckIntervalMs = 200,
            MaxIntervalMinutes = 300,
            MinWeightDiff = 1m
        };

        // Act
        var isValid = config.IsValid();

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void Should_Test_SystemSettings_Defaults()
    {
        // Arrange & Act
        var settings = new SystemSettings();

        // Assert - 验证默认值
        settings.EnableAutoStart.ShouldBeFalse();
        settings.CaptureStreamType.ShouldBe(StreamType.Substream);
        settings.LprDeviceType.ShouldBe(LprDeviceType.Hikvision);
        settings.MinDiffCharCount.ShouldBe(0);
    }

    [Fact]
    public void Should_Test_Configuration_Injection()
    {
        // Arrange
        var config = new WeighingConfiguration
        {
            MinWeightThreshold = 1.5m,
            WeightStabilityThreshold = 0.08m
        };

        var options = Options.Create(config);

        // Act
        var retrievedConfig = options.Value;

        // Assert
        retrievedConfig.ShouldBeSameAs(config);
        retrievedConfig.MinWeightThreshold.ShouldBe(1.5m);
    }
}

#endregion

#region Example 7: Scenario-Specific Test Modules

/// <summary>
/// 为特定场景创建测试模块
/// 适用于复杂的集成测试场景
/// </summary>
[DependsOn(typeof(MaterialClientCommonModule))]
public class HighPrecisionWeighingScenarioModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Scenario: 高精度称重场景
        var highPrecisionConfig = new WeighingConfiguration
        {
            MinWeightThreshold = 0.1m,      // 更低的最小重量
            WeightStabilityThreshold = 0.01m,  // 更高的精度要求
            StabilityWindowMs = 6000,      // 更长的稳定时间
            StabilityCheckIntervalMs = 100,    // 更频繁的检查
            MaxIntervalMinutes = 120,       // 更短的时间窗口
            MinWeightDiff = 0.5m           // 更小的重量差
        };

        context.Services.Replace(
            ServiceDescriptor.Singleton(
                typeof(IOptions<WeighingConfiguration>),
                Options.Create(highPrecisionConfig)
            )
        );
    }
}

[DependsOn(typeof(MaterialClientCommonModule))]
public class HighThroughputScenarioModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Scenario: 高吞吐量场景（快速称重）
        var fastThroughputConfig = new WeighingConfiguration
        {
            MinWeightThreshold = 1.0m,
            WeightStabilityThreshold = 0.1m,  // 较宽松的稳定性
            StabilityWindowMs = 2000,      // 更短的稳定时间
            StabilityCheckIntervalMs = 500,    // 较少频率的检查
            MaxIntervalMinutes = 600,       // 更长的时间窗口
            MinWeightDiff = 2.0m           // 更大的重量差
        };

        context.Services.Replace(
            ServiceDescriptor.Singleton(
                typeof(IOptions<WeighingConfiguration>),
                Options.Create(fastThroughputConfig)
            )
        );
    }
}

public class ScenarioBasedTests
{
    [Fact]
    public void Should_Test_High_Precision_Scenario()
    {
        // 使用高精度场景模块测试
        // var test = new MaterialClientTestBase<HighPrecisionWeighingScenarioModule>();
    }

    [Fact]
    public void Should_Test_High_Throughput_Scenario()
    {
        // 使用高吞吐量场景模块测试
        // var test = new MaterialClientTestBase<HighThroughputScenarioModule>();
    }
}

#endregion

