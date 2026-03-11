# MaterialClient.UI.Test

UI 测试基础设施 - 基于 Avalonia.Headless.XUnit 的 UI 组件自动化测试项目。

## 项目概述

此项目提供 MaterialClient.UI 项目的自动化测试基础设施，支持在无图形界面的环境中运行 UI 测试。

## 测试框架

- **xUnit** - 测试运行框架
- **Avalonia.Headless.XUnit** - Avalonia UI 无头测试支持
- **NSubstitute** - Mock 框架
- **Shouldly** - 断言库
- **coverlet.collector** - 测试覆盖率收集

## 测试结构

```
MaterialClient.UI.Test/
├── TestAppBuilder.cs       # Avalonia 应用构建器配置
├── TestStartup.cs          # 测试启动配置
├── TestHelper.cs          # 测试辅助工具
├── Mocks/               # Mock 工厂和辅助类
├── ViewModels/           # ViewModel 单元测试
├── Converters/           # Converter 单元测试
├── Controls/             # 自定义控件测试
└── Integration/          # 集成测试
```

## 运行测试

### 运行所有测试

```bash
dotnet test
```

### 运行特定测试项目

```bash
dotnet test --filter "FullyQualifiedName~ViewModels"
```

### 生成测试覆盖率报告

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## 编写测试

### ViewModel 测试示例

```csharp
using NSubstitute;
using Shouldly;
using Xunit;

namespace MaterialClient.UI.Test.ViewModels;

public class LoginViewModelTests
{
    [Fact]
    public void Constructor_WithValidDependencies_InitializesCorrectly()
    {
        // Arrange
        var authService = Substitute.For<IAuthService>();

        // Act
        var viewModel = new LoginViewModel(authService);

        // Assert
        viewModel.ShouldNotBeNull();
        viewModel.Username.ShouldBeNullOrEmpty();
    }
}
```

### Converter 测试示例

```csharp
using Shouldly;
using Xunit;
using MaterialClient.UI.Converters;

namespace MaterialClient.UI.Test.Converters;

public class NullOrEmptyImageConverterTests
{
    private readonly NullOrEmptyImageConverter _converter;

    public NullOrEmptyImageConverterTests()
    {
        _converter = new NullOrEmptyImageConverter();
    }

    [Fact]
    public void Convert_WithNullValue_ReturnsPlaceholderImage()
    {
        // Act
        var result = _converter.Convert(null, typeof(Bitmap), null, null);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<Bitmap>();
    }
}
```

## CI/CD 集成

测试可以在无头环境中运行，适用于持续集成管道。确保 CI/CD 环境满足以下要求：

- .NET SDK 10.0 或更高版本
- 无需图形界面（无头模式）

## 已知限制

1. **依赖问题**：某些运行时依赖（如 `Aliyun.OSS.SDK.NetCore`）可能导致测试环境问题。建议在需要时使用 Mock 替代实际服务。

2. **测试覆盖**：当前仅包含基础测试基础设施，完整测试需要在 UI 代码迁移后添加。

3. **阶段 3 依赖**：完整的 ViewModel、Converter 和控件测试需要 `separate-ui-to-materialclient-ui` 变更完成后，将 UI 代码迁移到 MaterialClient.UI 项目。

## 下一步

1. 等待 `separate-ui-to-materialclient-ui` 变更完成
2. 为 ViewModel 添加单元测试
3. 为 Converter 添加单元测试
4. 为自定义控件添加逻辑测试
5. 添加集成测试
6. 配置测试覆盖率目标和报告
7. 集成到 CI/CD 管道
