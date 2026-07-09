using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MaterialClient.Recycle.Services;
using MaterialClient.Views.AttendedWeighing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;

namespace MaterialClient.Recycle;

public class App : Application
{
    private IAbpApplicationWithInternalServiceProvider? _abpApplication;

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

    private async void OnApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        var logger = _abpApplication?.ServiceProvider.GetService<ILogger<App>>();
        logger?.LogInformation("Recycle application exit triggered, starting cleanup...");
        var totalSw = Stopwatch.StartNew();

        try
        {
            var shutdownTask = Task.Run(async () =>
            {
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
