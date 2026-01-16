# Test Configuration Guide

## 推荐方式：在 Scenario 初始化时通过 Replace IOptions<XXX> 配置

本文档展示 MaterialClient 测试项目中配置的最佳实践。

---

## 核心原则

### ✅ 推荐方式

**在测试场景初始化时直接替换 `IOptions<T>`**

```csharp
public class MyTests : MaterialClientTestBase<MaterialClientTestModule>
{
    [Fact]
    public void Should_Test_With_Custom_Config()
    {
        // 1. 创建自定义配置对象
        var customConfig = new WeighingConfiguration
        {
            MinWeightThreshold = 1.0m,
            WeightStabilityThreshold = 0.1m,
            StabilityWindowMs = 5000
        };

        // 2. 创建 IOptions<T>
        var options = Options.Create(customConfig);

        // 3. 直接在测试中使用
        var service = new YourServiceUnderTest(options);

        // 或者替换到 ServiceProvider（如果需要）
        // ServiceProvider.GetRequiredService<IServiceCollection>()
        //     .Replace(ServiceDescriptor.Singleton(typeof(IOptions<WeighingConfiguration>), options));
    }
}
```

### ❌ 不推荐方式

**依赖 `appsettings.json` 文件**

```csharp
// ❌ 不推荐：文件依赖
var builder = new ConfigurationBuilder();
builder.AddJsonFile("appsettings.json", false);
```

**问题**：
- 文件 I/O 开销
- 测试之间共享配置
- "文件未找到"错误
- CI/CD 环境路径问题
- 配置不直观

---

## 使用场景

### 场景 1：单个测试需要特定配置

**适用情况**：测试方法需要特定的配置值

```csharp
[Fact]
public void Should_Test_With_Strict_Weight_Stability()
{
    // 创建测试特定配置
    var strictConfig = new WeighingConfiguration
    {
        MinWeightThreshold = 0.5m,
        WeightStabilityThreshold = 0.01m,  // 更严格
        StabilityWindowMs = 5000,
        StabilityCheckIntervalMs = 100
    };

    // 验证配置有效
    strictConfig.IsValid().ShouldBeTrue();

    // 使用配置进行测试
    // ...
}
```

### 场景 2：一组测试共享配置

**适用情况**：多个测试使用相同的配置

**方法 1：创建专用测试模块**

```csharp
[DependsOn(typeof(MaterialClientCommonModule))]
public class HighPrecisionWeighingScenarioModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var highPrecisionConfig = new WeighingConfiguration
        {
            MinWeightThreshold = 0.1m,
            WeightStabilityThreshold = 0.01m,
            StabilityWindowMs = 6000
        };

        // 在模块级别替换配置
        context.Services.Replace(
            ServiceDescriptor.Singleton(
                typeof(IOptions<WeighingConfiguration>),
                Options.Create(highPrecisionConfig)
            )
        );
    }
}

public class HighPrecisionTests : MaterialClientTestBase<HighPrecisionWeighingScenarioModule>
{
    [Fact]
    public void Should_Test_High_Precision_Scenario()
    {
        var options = ServiceProvider.GetRequiredService<IOptions<WeighingConfiguration>>();
        options.Value.WeightStabilityThreshold.ShouldBe(0.01m);
    }
}
```

### 场景 3：单元测试（无需完整 ABP 环境）

**适用情况**：纯业务逻辑测试

```csharp
public class WeighingConfigurationTests
{
    [Fact]
    public void Should_Validate_Valid_Configuration()
    {
        var config = new WeighingConfiguration
        {
            MinWeightThreshold = 0.5m,
            WeightStabilityThreshold = 0.05m,
            StabilityWindowMs = 3000,
            StabilityCheckIntervalMs = 200,
            MaxIntervalMinutes = 300,
            MinWeightDiff = 1m
        };

        config.IsValid().ShouldBeTrue();
    }

    [Fact]
    public void Should_Reject_Invalid_Configuration()
    {
        var config = new WeighingConfiguration
        {
            MinWeightThreshold = -1m,  // 无效值
            WeightStabilityThreshold = 0.05m
        };

        config.IsValid().ShouldBeFalse();
    }
}
```

---

## 配置类型示例

### 1. WeighingConfiguration

```csharp
var weighingConfig = new WeighingConfiguration
{
    MinWeightThreshold = 0.5m,      // 最小称重重量（吨）
    WeightStabilityThreshold = 0.05m, // 重量稳定性阈值（吨）
    StabilityWindowMs = 3000,        // 稳定性监控窗口（毫秒）
    StabilityCheckIntervalMs = 200,  // 检查间隔（毫秒）
    MaxIntervalMinutes = 300,        // 最大时间间隔（分钟）
    MinWeightDiff = 1m               // 最小重量差（吨）
};
```

### 2. SystemSettings

```csharp
var systemSettings = new SystemSettings
{
    EnableAutoStart = true,
    CaptureStreamType = StreamType.Mainstream,
    Urls = "http://test-server:8080",
    SnapshotCameraType = SnapshotCameraType.Hikvision,
    MinDiffCharCount = 1
};
```

---

## 最佳实践

### ✅ DO

1. **在测试中直接创建配置对象**
   ```csharp
   var config = new WeighingConfiguration { ... };
   ```

2. **使用 `Options.Create()` 包装配置**
   ```csharp
   var options = Options.Create(config);
   ```

3. **验证配置有效性**
   ```csharp
   config.IsValid().ShouldBeTrue();
   ```

4. **为不同场景创建独立配置**
   ```csharp
   var test1Config = new WeighingConfiguration { ... };
   var test2Config = new WeighingConfiguration { ... };
   // 每个测试独立配置
   ```

5. **使用描述性的测试类和模块名称**
   ```csharp
   public class HighPrecisionWeighingScenarioModule : AbpModule
   public class StrictWeightStabilityTests : MaterialClientTestBase<...>
   ```

### ❌ DON'T

1. **不要依赖 `appsettings.json`**
   ```csharp
   // ❌ 避免
   builder.AddJsonFile("appsettings.json", false);
   ```

2. **不要在测试之间共享可变配置**
   ```csharp
   // ❌ 避免：静态共享配置
   private static readonly sharedConfig = new WeighingConfiguration();
   ```

3. **不要在配置中使用外部资源**
   ```csharp
   // ❌ 避免
   builder.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ENV")}.json");
   ```

---

## 迁移指南

### 从文件配置迁移到内存配置

#### Before (File-based)

```csharp
// appsettings.json
{
  "WeighingConfiguration": {
    "MinWeightThreshold": 0.5,
    "WeightStabilityThreshold": 0.05
  }
}

// TestBase.cs
protected override void BeforeAddApplication(IServiceCollection services)
{
    var builder = new ConfigurationBuilder();
    builder.AddJsonFile("appsettings.json", false);
    services.ReplaceConfiguration(builder.Build());
}
```

#### After (In-memory)

```csharp
// TestBase.cs
protected override void BeforeAddApplication(IServiceCollection services)
{
    var inMemorySettings = new Dictionary<string, string>
    {
        ["ConnectionStrings:Default"] = "Data Source=:memory:",
        ["BasePlatform:BaseUrl"] = "http://test-base.publicapi.findong.com",
        ["BasePlatform:ProductCode"] = "5000",
        ["Encryption:AesKey"] = "MTIzNDU2Nzg5MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTI="
    };

    var builder = new ConfigurationBuilder();
    builder.AddInMemoryCollection(inMemorySettings);
    services.ReplaceConfiguration(builder.Build());
}

// Specific test
[Fact]
public void Should_Test_With_Custom_Config()
{
    var customConfig = new WeighingConfiguration
    {
        MinWeightThreshold = 1.0m,
        WeightStabilityThreshold = 0.1m
    };

    var options = Options.Create(customConfig);
    // 使用 options 进行测试
}
```

---

## 完整示例

### 场景：测试不同精度的称重配置

```csharp
using MaterialClient.Common.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

public class WeighingConfigurationScenarioTests
{
    [Fact]
    public void Scenario_High_Precision_Weighing()
    {
        // Arrange: 高精度称重场景
        var config = new WeighingConfiguration
        {
            MinWeightThreshold = 0.1m,      // 更低的最小重量
            WeightStabilityThreshold = 0.01m,  // 更高的精度
            StabilityWindowMs = 6000,      // 更长的稳定时间
            StabilityCheckIntervalMs = 100,    // 更频繁的检查
            MaxIntervalMinutes = 120,
            MinWeightDiff = 0.5m
        };

        // Act & Assert
        config.IsValid().ShouldBeTrue();
        config.WeightStabilityThreshold.ShouldBe(0.01m);
        config.StabilityCheckIntervalMs.ShouldBe(100);
    }

    [Fact]
    public void Scenario_High_Throughput_Weighing()
    {
        // Arrange: 高吞吐量称重场景
        var config = new WeighingConfiguration
        {
            MinWeightThreshold = 1.0m,
            WeightStabilityThreshold = 0.1m,  // 较宽松的稳定性
            StabilityWindowMs = 2000,      // 更短的稳定时间
            StabilityCheckIntervalMs = 500,
            MaxIntervalMinutes = 600,
            MinWeightDiff = 2.0m
        };

        // Act & Assert
        config.IsValid().ShouldBeTrue();
        config.WeightStabilityThreshold.ShouldBe(0.1m);
        config.StabilityWindowMs.ShouldBe(2000);
    }

    [Fact]
    public void Scenario_Edge_Case_Minimum_Values()
    {
        // Arrange: 边界条件测试
        var config = new WeighingConfiguration
        {
            MinWeightThreshold = 0.01m,    // 接近 0 但有效
            WeightStabilityThreshold = 0.001m,
            StabilityWindowMs = 1000,
            StabilityCheckIntervalMs = 50,
            MaxIntervalMinutes = 1,
            MinWeightDiff = 0.1m
        };

        // Act & Assert
        config.IsValid().ShouldBeTrue();
    }

    [Fact]
    public void Scenario_Invalid_Configuration_Should_Fail_Validation()
    {
        // Arrange: 无效配置
        var config = new WeighingConfiguration
        {
            MinWeightThreshold = -1m,  // 无效：负值
            WeightStabilityThreshold = 0.05m
        };

        // Act & Assert
        config.IsValid().ShouldBeFalse();
    }
}
```

---

## 参考资源

- **示例代码**: `ConfigurationTestExamples.cs` - 包含 7 个不同的配置场景示例
- **配置类**: `MaterialClient.Common.Configuration` 命名空间
- **ABP 文档**: [ABP Testing Documentation](https://docs.abp.io/en/abp/latest/Testing)

---

## 常见问题

### Q: 什么时候使用 Module 级别的配置替换？

A: 当多个测试类需要共享相同的配置时。例如：
- `HighPrecisionScenarioModule` - 所有高精度测试
- `FastThroughputScenarioModule` - 所有快速称重测试

### Q: 什么时候直接在测试方法中创建配置？

A: 当单个测试需要特定配置时。这是最常见和推荐的方式。

### Q: 是否还需要 `appsettings.json` 文件？

A: 不需要。测试配置应该完全在内存中创建，更加快速和可靠。

### Q: 如何模拟外部服务配置？

A: 在测试场景中创建配置对象，使用测试 URL 和端点：

```csharp
var config = new SystemSettings
{
    Urls = "http://mock-test-server.local:8080"
};
```

---

## 总结

**推荐方式的核心优势**：

✅ **更快** - 无文件 I/O
✅ **更可靠** - 无文件依赖
✅ **更清晰** - 配置在代码中可见
✅ **更灵活** - 每个测试独立配置
✅ **更隔离** - 测试之间不共享状态

**记住**：在测试中直接创建配置对象，使用 `Options.Create()` 包装，然后传递给被测试的服务。
