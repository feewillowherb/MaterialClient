using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Urban.Services;
using MaterialClient.Urban.ViewModels;
using MaterialClient.Urban.Views;
using MaterialClient.Urban.Views.Dialogs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;

namespace MaterialClient.Urban;

public class App : Application
{
    private IAbpApplicationWithInternalServiceProvider? _abpApplication;
    private UrbanAttendedWeighingViewModel? _viewModel;
    private IMinimalWebHostService? _minimalWebHostService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 未授权流程中会临时切换 MainWindow；显式退出，避免关闭提示窗时整个进程被关掉
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                // Create and initialize ABP application with Autofac (matching MaterialClient pattern)
                _abpApplication = await AbpApplicationFactory.CreateAsync<MaterialClientUrbanModule>(options =>
                {
                    options.UseAutofac();
                });

                await _abpApplication.InitializeAsync();

                var startupAuth = _abpApplication.ServiceProvider
                    .GetRequiredService<IUrbanStartupAuthorizationService>();
                if (!startupAuth.IsAuthorized)
                {
                    var shouldContinue = await HandleUnauthorizedStartupAsync(desktop, _abpApplication);
                    if (!shouldContinue)
                    {
                        desktop.Exit += OnApplicationExit;
                        return;
                    }
                }

                // Resolve window from ABP container
                var window = _abpApplication.ServiceProvider.GetRequiredService<UrbanAttendedWeighingWindow>();
                _viewModel = window.ViewModel;

                // After async ABP init, MainWindow must be shown explicitly (see MaterialClient App.axaml.cs).
                desktop.MainWindow = window;
                window.Show();
                StartMainWindowServices();

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

    private async Task<bool> HandleUnauthorizedStartupAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        IAbpApplicationWithInternalServiceProvider abpApplication)
    {
        var startupAuth = abpApplication.ServiceProvider
            .GetRequiredService<IUrbanStartupAuthorizationService>();
        var notice = new UnauthorizedNoticeWindow(startupAuth.Result.FailureMessage);
        desktop.MainWindow = notice;
        notice.Show();

        try
        {
            while (true)
            {
                var userChoice = await AwaitNoticeChoiceAsync(notice);

                if (userChoice == UnauthorizedNoticeWindow.UnauthorizedNoticeResult.Exit)
                {
                    desktop.Shutdown();
                    return false;
                }

                var activationWindow = abpApplication.ServiceProvider
                    .GetRequiredService<UrbanActivationWindow>();
                var activated = await activationWindow.ShowDialog<bool?>(notice);
                if (activated != true)
                {
                    continue;
                }

                var licenseService = abpApplication.ServiceProvider.GetRequiredService<ILicenseService>();
                if (await licenseService.IsLicenseValidAsync())
                {
                    return true;
                }
            }
        }
        finally
        {
            if (notice.IsVisible)
            {
                notice.Close();
            }
        }
    }

    private static Task<UnauthorizedNoticeWindow.UnauthorizedNoticeResult> AwaitNoticeChoiceAsync(
        UnauthorizedNoticeWindow notice)
    {
        var choiceTcs = new TaskCompletionSource<UnauthorizedNoticeWindow.UnauthorizedNoticeResult>();

        void OnOnlineActivateRequested(object? sender, EventArgs e)
        {
            Cleanup();
            choiceTcs.TrySetResult(UnauthorizedNoticeWindow.UnauthorizedNoticeResult.OnlineActivate);
        }

        void OnNoticeClosed(object? sender, EventArgs e)
        {
            if (choiceTcs.Task.IsCompleted)
            {
                return;
            }

            Cleanup();
            choiceTcs.TrySetResult(notice.UserChoice);
        }

        void Cleanup()
        {
            notice.OnlineActivateRequested -= OnOnlineActivateRequested;
            notice.Closed -= OnNoticeClosed;
        }

        notice.OnlineActivateRequested += OnOnlineActivateRequested;
        notice.Closed += OnNoticeClosed;
        return choiceTcs.Task;
    }

    private void StartMainWindowServices()
    {
        if (_abpApplication == null)
        {
            return;
        }

        try
        {
            _viewModel?.Initialize();
            _minimalWebHostService = _abpApplication.ServiceProvider.GetService<IMinimalWebHostService>();
            _ = StartUrbanWebHostAsync();
            _ = StartDevicesAndStatusMonitoringAsync();

            var logger = _abpApplication.ServiceProvider.GetService<ILogger<App>>();
            logger?.LogInformation("Urban ViewModel initialized, device status monitoring started");
        }
        catch (Exception ex)
        {
            var logger = _abpApplication.ServiceProvider.GetService<ILogger<App>>();
            logger?.LogError(ex, "Failed to initialize ViewModel");
        }
    }

    private async Task StartUrbanWebHostAsync()
    {
        if (_abpApplication == null)
        {
            return;
        }

        var configuration = _abpApplication.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = _abpApplication.ServiceProvider.GetService<ILogger<App>>();

        var enableOnStartup = configuration.GetValue("UrbanWebHost:EnableOnStartup", true);
        if (!enableOnStartup)
        {
            logger?.LogInformation("Urban minimal web host startup is disabled by configuration.");
            return;
        }

        try
        {
            if (_minimalWebHostService != null && !_minimalWebHostService.IsRunning)
            {
                await _minimalWebHostService.StartAsync();
            }

        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to start urban minimal web host");
        }
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

                // 2. Stop web host first to avoid pending web callbacks during shutdown
                if (_minimalWebHostService != null)
                {
                    try
                    {
                        await _minimalWebHostService.StopAsync();
                        logger?.LogInformation("Urban minimal web host stopped");
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Error stopping urban minimal web host");
                    }
                }

                // 3. Close hardware devices explicitly (before ABP shutdown)
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

                // 4. Shutdown ABP application
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
