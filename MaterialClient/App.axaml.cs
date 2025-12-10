using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Threading.Tasks;
using System;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MaterialClient.ViewModels;
using MaterialClient.Views;
using MaterialClient.Services;
using Volo.Abp;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient;

public partial class App : Application
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
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
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
                        System.Diagnostics.Debug.WriteLine($"Web host startup error: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    }
                });

                // Run startup flow
                var startupService = _abpApplication.ServiceProvider.GetRequiredService<StartupService>();
                var mainWindow = await startupService.StartupAsync();

                if (mainWindow != null)
                {
                    desktop.MainWindow = mainWindow;
                    
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
                System.Diagnostics.Debug.WriteLine($"Startup error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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
            // Stop Web Host service
            if (_webHostService != null)
            {
                await _webHostService.StopAsync();
            }

            // Shutdown ABP application
            if (_abpApplication != null)
            {
                await _abpApplication.ShutdownAsync();
                _abpApplication.Dispose();
            }
        }).Wait();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}