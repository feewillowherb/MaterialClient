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

namespace MaterialClient;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            
            // Wait for ServiceLocator to be initialized, then run startup flow
            Task.Run(async () =>
            {
                // Wait for ServiceLocator initialization (max 10 seconds)
                var maxWaitTime = TimeSpan.FromSeconds(10);
                var startTime = DateTime.Now;
                
                while ((DateTime.Now - startTime) < maxWaitTime)
                {
                    try
                    {
                        // Try to get a service to check if ServiceLocator is ready
                        var licenseService = ServiceLocator.GetService<MaterialClient.Common.Services.Authentication.ILicenseService>();
                        if (licenseService != null)
                        {
                            // ServiceLocator is ready
                            break;
                        }
                    }
                    catch
                    {
                        // Not ready yet
                    }
                    
                    await Task.Delay(100);
                }
                
                // Run startup flow on UI thread
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        var licenseService = ServiceLocator.GetRequiredService<MaterialClient.Common.Services.Authentication.ILicenseService>();
                        var authService = ServiceLocator.GetRequiredService<MaterialClient.Common.Services.Authentication.IAuthenticationService>();
                        
                        var startupService = new StartupService(licenseService, authService);
                        var mainWindow = await startupService.StartupAsync();
                        
                        if (mainWindow != null)
                        {
                            desktop.MainWindow = mainWindow;
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
                        desktop.Shutdown();
                    }
                });
            });
        }

        base.OnFrameworkInitializationCompleted();
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