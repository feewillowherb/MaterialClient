using Avalonia.Headless;

namespace MaterialClient.UI.Test;

/// <summary>
/// Test startup configuration for Avalonia Headless.
/// </summary>
public static class TestStartup
{
    /// <summary>
    /// Gets the Avalonia application builder for testing.
    /// </summary>
    public static Avalonia.AppBuilder Build() => TestAppBuilder.BuildAvaloniaApp();
}
