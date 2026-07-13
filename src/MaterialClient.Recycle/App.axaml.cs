using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MaterialClient.Recycle.Services;
using MaterialClient.Views.AttendedWeighing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;

namespace MaterialClient.Recycle;

public class App : Application
{
    private IAbpApplicationWithInternalServiceProvider? _abpApplication;
    private IMinimalWebHostService? _minimalWebHostService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                _abpApplication = await AbpApplicationFactory.CreateAsync<MaterialClientRecycleModule>(options =>
                {
                    options.UseAutofac();
                });

                await _abpApplication.InitializeAsync();
                desktop.Exit += OnApplicationExit;

                var logger = _abpApplication.ServiceProvider.GetService<ILogger<App>>();

                // 诊断 Web Host：根据 MinimalWebHost:EnableOnStartup 控制是否启动
                _ = StartMinimalWebHostAsync();

                var startupService = _abpApplication.ServiceProvider.GetRequiredService<RecycleStartupService>();
                var mainWindow = await startupService.StartupAsync();

                if (mainWindow == null)
                {
                    logger?.LogWarning("Recycle 启动流程未完成（授权或登录取消），退出应用。");
                    desktop.Shutdown();
                    return;
                }

                logger?.LogInformation("Recycle 启动完成，显示称重主界面。");
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                desktop.MainWindow = mainWindow;
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Recycle] ABP initialization error: {ex.Message}");
                desktop.Shutdown();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartMinimalWebHostAsync()
    {
        if (_abpApplication == null)
        {
            return;
        }

        var configuration = _abpApplication.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = _abpApplication.ServiceProvider.GetService<ILogger<App>>();

        var enableOnStartup = configuration.GetValue("MinimalWebHost:EnableOnStartup", true);
        if (!enableOnStartup)
        {
            logger?.LogInformation("Recycle minimal web host startup is disabled by configuration.");
            return;
        }

        try
        {
            _minimalWebHostService = _abpApplication.ServiceProvider.GetService<IMinimalWebHostService>();
            if (_minimalWebHostService != null && !_minimalWebHostService.IsRunning)
            {
                await _minimalWebHostService.StartAsync();
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to start recycle minimal web host");
        }
    }

    private async void OnApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        var logger = _abpApplication?.ServiceProvider.GetService<ILogger<App>>();
        logger?.LogInformation("Recycle application exit triggered, starting cleanup...");
        var totalSw = Stopwatch.StartNew();

        try
        {
            var shutdownTask = Task.Run(async () =>
            {
                // 先停止 Web Host，避免关闭期间仍有未完成的回调
                if (_minimalWebHostService != null)
                {
                    try
                    {
                        await _minimalWebHostService.StopAsync();
                        logger?.LogInformation("Recycle minimal web host stopped");
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Error stopping recycle minimal web host");
                    }
                }

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
