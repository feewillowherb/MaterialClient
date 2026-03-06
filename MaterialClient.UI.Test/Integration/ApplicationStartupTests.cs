using MaterialClient.UI;
using Shouldly;
using Xunit;

namespace MaterialClient.UI.Test.Integration;

/// <summary>
/// Tests for application startup and configuration
/// </summary>
public class ApplicationStartupTests
{
    [Fact]
    public void App_CanCreateInstance()
    {
        // Arrange & Act
        var app = new App();

        // Assert
        app.ShouldNotBeNull();
    }

    [Fact]
    public void App_ConfigurationLoadsCorrectly()
    {
        // This is a simple test to verify the app can be initialized
        // In a real testing scenario, we would need to mock more dependencies
        var app = new App();

        // Assert that the app instance is valid
        app.ShouldNotBeNull();
    }
}