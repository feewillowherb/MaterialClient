using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using MaterialClient.Common.Services;
using MaterialClient.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;

namespace MaterialClient;

public class App : Application
{
    private IAbpApplicationWithInternalServiceProvider? _abpApplication;
    private MinimalWebHostService? _webHostService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
#if DEBUG
            this.AttachDeveloperTools();
#endif


            // Avoid duplicate validations from both Avalonia and MVVM frameworks (ReactiveUI). 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            try
            {
                // Create and initialize ABP application with Autofac
                // ABP framework will automatically load appsettings.json from the application base directory
                _abpApplication = await AbpApplicationFactory.CreateAsync<MaterialClientModule>(options =>
                {
                    options.UseAutofac();
                });

                await _abpApplication.InitializeAsync();

                // Get Web Host service and start in background
                _webHostService = _abpApplication.ServiceProvider.GetRequiredService<MinimalWebHostService>();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _webHostService.StartAsync();
                    }
                    catch (Exception ex)
                    {
                        var logger = _abpApplication?.ServiceProvider.GetService<ILogger<App>>();
                        logger?.LogError(ex, "Web host 启动错误");
                    }
                });

                // Run startup flow
                var startupService = _abpApplication.ServiceProvider.GetRequiredService<StartupService>();
                var mainWindow = await startupService.StartupAsync();

                if (mainWindow != null)
                {
                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();

                    // Register exit handler
                    desktop.Exit += OnApplicationExit;
                }
                else
                {
                    // Startup failed or user cancelled - exit application
                    desktop.Shutdown();
                }
            }
            catch (Exception ex)
            {
                var logger = _abpApplication?.ServiceProvider.GetService<ILogger<App>>();
                logger?.LogError(ex, "启动错误");
                desktop.Shutdown();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        var logger = _abpApplication?.ServiceProvider.GetService<ILogger<App>>();
        logger?.LogInformation("应用程序退出事件触发，开始清理资源...");
        var totalSw = Stopwatch.StartNew();

        try
        {
            var shutdownTask = Task.Run(async () =>
            {
                // 1. Stop Web Host service
                if (_webHostService != null && _webHostService.IsRunning)
                {
                    var sw = Stopwatch.StartNew();
                    logger?.LogInformation("正在停止 Web Host 服务...");
                    try
                    {
                        await _webHostService.DisposeAsync();
                        logger?.LogInformation("Web Host 服务已停止 ({ElapsedMs}ms)", sw.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "停止 Web Host 服务时出错");
                    }
                }

                // 2. Close hardware devices explicitly (before ABP shutdown)
                if (_abpApplication != null)
                {
                    var sw = Stopwatch.StartNew();
                    logger?.LogInformation("正在关闭硬件设备...");
                    try
                    {
                        var deviceManager = _abpApplication.ServiceProvider.GetService<IDeviceManagerService>();
                        if (deviceManager != null)
                        {
                            await deviceManager.CloseAsync();
                            logger?.LogInformation("硬件设备已关闭 ({ElapsedMs}ms)", sw.ElapsedMilliseconds);
                        }
                        else
                        {
                            logger?.LogWarning("IDeviceManagerService 未注册，跳过硬件关闭");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "关闭硬件设备时出错");
                    }
                }

                // 3. Shutdown ABP application
                if (_abpApplication != null)
                {
                    var sw = Stopwatch.StartNew();
                    logger?.LogInformation("正在关闭 ABP 应用程序...");
                    try
                    {
                        await _abpApplication.ShutdownAsync();
                        _abpApplication.Dispose();
                        _abpApplication = null;
                        logger?.LogInformation("ABP 应用程序已关闭 ({ElapsedMs}ms)", sw.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "关闭 ABP 应用程序时出错");
                    }
                }

                logger?.LogInformation("资源清理完成 (总计 {TotalMs}ms)", totalSw.ElapsedMilliseconds);
            });

            if (!shutdownTask.Wait(TimeSpan.FromSeconds(10)))
            {
                logger?.LogWarning("资源清理超时（10秒），强制退出");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "应用程序退出时发生错误，强制退出");
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove) BindingPlugins.DataValidators.Remove(plugin);
    }
}