using Avalonia;
using Xunit;

namespace MaterialClient.UI.Test;

/// <summary>
/// Tests for TestAppBuilder to verify configuration
/// </summary>
public class TestAppBuilderTests
{
    [Fact]
    public void BuildAvaloniaApp_ShouldReturnAppBuilder()
    {
        // Arrange & Act
        var appBuilder = TestAppBuilder.BuildAvaloniaApp();

        // Assert
        Assert.NotNull(appBuilder);
    }

    [Fact(Skip = "Headless 完整初始化由 [AvaloniaTestApplication] 在程序集加载时执行；单独调用 SetupWithoutStarting() 会因渲染未注入而失败。其他测试已通过 Headless 运行。")]
    public void TestInfrastructure_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var app = TestAppBuilder.BuildAvaloniaApp()
            .SetupWithoutStarting();

        // Assert
        Assert.NotNull(app);
        Assert.NotNull(app.Instance);
    }
}
