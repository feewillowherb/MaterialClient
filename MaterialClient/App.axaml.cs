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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient;

public partial class App : Application
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
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            try
            {
                // Load configuration from appsettings.json
                var builder = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                var configuration = builder.Build();

                // Create and initialize ABP application with Autofac
                _abpApplication = await AbpApplicationFactory.CreateAsync<MaterialClientModule>(options =>
                {
                    options.UseAutofac();
                    options.Services.ReplaceConfiguration(configuration);
                });

                await _abpApplication.InitializeAsync();

                // Run startup flow
                var startupService = _abpApplication.ServiceProvider.GetRequiredService<StartupService>();
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
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                desktop.Shutdown();
            }
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