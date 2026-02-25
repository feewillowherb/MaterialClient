using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
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
            this.AttachDevTools();
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

        try
        {
            // 使用超时机制避免死锁，最多等待 3 秒
            var shutdownTask = Task.Run(async () =>
            {
                // Stop Web Host service first
                if (_webHostService != null && _webHostService.IsRunning)
                {
                    logger?.LogInformation("正在停止 Web Host 服务...");
                    try
                    {
                        await _webHostService.DisposeAsync();
                        logger?.LogInformation("Web Host 服务已停止");
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "停止 Web Host 服务时出错");
                    }
                }

                // Shutdown ABP application
                if (_abpApplication != null)
                {
                    logger?.LogInformation("正在关闭 ABP 应用程序...");
                    try
                    {
                        await _abpApplication.ShutdownAsync();
                        _abpApplication.Dispose();
                        _abpApplication = null;
                        logger?.LogInformation("ABP 应用程序已关闭");
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "关闭 ABP 应用程序时出错");
                    }
                }

                logger?.LogInformation("资源清理完成");
            });

            // 使用超时机制，避免无限等待
            if (!shutdownTask.Wait(TimeSpan.FromSeconds(3)))
            {
                logger?.LogWarning("资源清理超时，强制退出");
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