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

    [Fact]
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
