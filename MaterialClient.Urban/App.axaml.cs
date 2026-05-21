using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.AttendedWeighing;
using MaterialClient.Urban.Services;
using MaterialClient.Urban.ViewModels;
using MaterialClient.Urban.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MaterialClient.Urban;

public class App : Application
{
    private IAttendedWeighingService? _attendedWeighingService;
    private WeighingSystemViewModel? _viewModel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Build service collection for Urban mode (no ABP container, manual DI)
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

            // Register IWeighingPipelineStrategy → UrbanWeighingPipelineStrategy
            services.AddSingleton<IWeighingPipelineStrategy, UrbanWeighingPipelineStrategy>();

            // Register IUrbanWeighingService
            services.AddSingleton<IUrbanWeighingService, UrbanWeighingService>();

            // Note: Other services (IAttendedWeighingService, repositories, etc.)
            // are expected to be registered by the ABP module initialization
            // from MaterialClient.Common. For standalone Urban mode, the
            // ViewModel receives them via constructor injection.

            // Urban: Perform static license check in background (no UI exposure)
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
                    Console.Error.WriteLine($"[Urban] 静态授权检查异常: {ex.Message}");
                }
            });

            // Urban: Directly open the weighing main window (no login, no authorization UI)
            var window = new WeighingSystemWindow();

            // Create ViewModel and wire up events
            // Note: In production, these dependencies come from ABP DI.
            // For now, we pass through the window's DataContext setup
            _viewModel = window.DataContext as WeighingSystemViewModel;
            _viewModel?.Initialize();

            desktop.MainWindow = window;

            // Start the attended weighing service after window is shown
            desktop.MainWindow.Opened += async (_, _) =>
            {
                try
                {
                    // The IAttendedWeighingService is resolved from the window's DataContext
                    // which should have received it via DI
                    if (_viewModel != null)
                    {
                        _viewModel.LoadDeviceStatuses();
                        Console.WriteLine("[Urban] ViewModel 初始化完成，设备状态已加载");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Urban] 启动称重服务失败: {ex.Message}");
                }
            };

            // Register exit handler for resource cleanup
            desktop.Exit += OnApplicationExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        try
        {
            _viewModel?.Dispose();

            if (_attendedWeighingService != null)
            {
                await _attendedWeighingService.StopAsync();
                await _attendedWeighingService.DisposeAsync();
            }

            Console.WriteLine("[Urban] 资源清理完成");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Urban] 资源清理异常: {ex.Message}");
        }
    }
}
