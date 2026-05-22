using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MaterialClient.Common.Services;
using MaterialClient.Urban.ViewModels;
using MaterialClient.Urban.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;

namespace MaterialClient.Urban;

public class App : Application
{
    private IAbpApplicationWithInternalServiceProvider? _abpApplication;
    private UrbanAttendedWeighingViewModel? _viewModel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                // Create and initialize ABP application with Autofac (matching MaterialClient pattern)
                _abpApplication = await AbpApplicationFactory.CreateAsync<MaterialClientUrbanModule>(options =>
                {
                    options.UseAutofac();
                });

                await _abpApplication.InitializeAsync();

                // Resolve window from ABP container
                var window = _abpApplication.ServiceProvider.GetRequiredService<UrbanAttendedWeighingWindow>();
                _viewModel = window.ViewModel;

                desktop.MainWindow = window;

                // Start ViewModel initialization after window is shown
                desktop.MainWindow.Opened += (_, _) =>
                {
                    try
                    {
                        _viewModel?.Initialize();
                        _ = StartDevicesAndStatusMonitoringAsync();
                        var logger = _abpApplication.ServiceProvider.GetService<ILogger<App>>();
                        logger?.LogInformation("Urban ViewModel initialized, device status monitoring started");
                    }
                    catch (Exception ex)
                    {
                        var logger = _abpApplication.ServiceProvider.GetService<ILogger<App>>();
                        logger?.LogError(ex, "Failed to initialize ViewModel");
                    }
                };

                // Register exit handler for resource cleanup
                desktop.Exit += OnApplicationExit;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Urban] ABP initialization error: {ex.Message}");
                desktop.Shutdown();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartDevicesAndStatusMonitoringAsync()
    {
        if (_abpApplication == null || _viewModel == null) return;

        try
        {
            var deviceManager = _abpApplication.ServiceProvider.GetService<IDeviceManagerService>();
            if (deviceManager != null)
            {
                await deviceManager.StartAsync();
            }

            var attendedWeighingService =
                _abpApplication.ServiceProvider.GetRequiredService<IAttendedWeighingService>();
            await attendedWeighingService.StartAsync();

            _viewModel.StartDeviceStatusMonitoring();
        }
        catch (Exception ex)
        {
            var logger = _abpApplication.ServiceProvider.GetService<ILogger<App>>();
            logger?.LogError(ex, "Failed to start devices or device status monitoring");
        }
    }

    private async void OnApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        var logger = _abpApplication?.ServiceProvider.GetService<ILogger<App>>();
        logger?.LogInformation("Application exit event triggered, starting cleanup...");
        var totalSw = Stopwatch.StartNew();

        try
        {
            var shutdownTask = Task.Run(async () =>
            {
                // 1. Dispose ViewModel (release event subscriptions)
                _viewModel?.Dispose();
                logger?.LogInformation("ViewModel disposed");

                // 2. Close hardware devices explicitly (before ABP shutdown)
                if (_abpApplication != null)
                {
                    try
                    {
                        var deviceManager = _abpApplication.ServiceProvider.GetService<IDeviceManagerService>();
                        if (deviceManager != null)
                        {
                            await deviceManager.CloseAsync();
                            logger?.LogInformation("Hardware devices closed");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Error closing hardware devices");
                    }
                }

                // 3. Shutdown ABP application
                if (_abpApplication != null)
                {
                    var sw = Stopwatch.StartNew();
                    logger?.LogInformation("Shutting down ABP application...");
                    try
                    {
                        await _abpApplication.ShutdownAsync();
                        _abpApplication.Dispose();
                        _abpApplication = null;
                        logger?.LogInformation("ABP application shut down ({ElapsedMs}ms)", sw.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Error shutting down ABP application");
                    }
                }

                logger?.LogInformation("Resource cleanup completed (total {TotalMs}ms)", totalSw.ElapsedMilliseconds);
            });

            if (!shutdownTask.Wait(TimeSpan.FromSeconds(10)))
            {
                logger?.LogWarning("Resource cleanup timed out (10s), forcing exit");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during application exit, forcing exit");
        }
    }
}
