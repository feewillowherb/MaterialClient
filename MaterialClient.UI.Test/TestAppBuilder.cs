using Avalonia;

namespace MaterialClient.UI.Test;

/// <summary>
/// Test application builder for Avalonia Headless testing.
/// Configures the application to run in a headless (no UI) environment.
/// </summary>
public static class TestAppBuilder
{
    /// <summary>
    /// Builds and configures the Avalonia application for headless testing.
    /// </summary>
    /// <returns>Configured AppBuilder instance</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>();
}
