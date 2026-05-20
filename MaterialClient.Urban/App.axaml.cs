using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Services;
using MaterialClient.Urban.Views;

namespace MaterialClient.Urban;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Urban: Perform static license check in background (no UI exposure)
            // Authorization result is logged only; does NOT block app startup
            _ = Task.Run(async () =>
            {
                try
                {
                    var checker = new StaticLicenseChecker();
                    var settings = new SystemSettings();
                    var result = await checker.CheckLicenseAsync(settings.LicenseFilePath);
                    Console.WriteLine($"[Urban] 静态授权检查: {(result.IsSuccess ? "通过" : "失败")} - {result.Message}");
                }
                catch (Exception ex)
                {
                    // Authorization check exception does NOT block app startup
                    Console.Error.WriteLine($"[Urban] 静态授权检查异常: {ex.Message}");
                }
            });

            // Urban: Directly open the weighing main window (no login, no authorization UI)
            desktop.MainWindow = new WeighingSystemWindow();

            // Register exit handler for resource cleanup
            desktop.Exit += OnApplicationExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        // TODO: Add resource cleanup logic when needed
        // e.g., stop hardware devices, save application state
    }
}
