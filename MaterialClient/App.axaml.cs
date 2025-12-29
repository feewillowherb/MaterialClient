using System;
using System.Linq;
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
        // Stop Web Host and ABP application synchronously
        Task.Run(async () =>
        {
            // Dispose Web Host service (will stop it if running)
            if (_webHostService != null && _webHostService.IsRunning == true) await _webHostService.DisposeAsync();

            // Shutdown ABP application
            if (_abpApplication != null)
            {
                await _abpApplication.ShutdownAsync();
                _abpApplication.Dispose();
                _abpApplication = null;
            }
        }).Wait();
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