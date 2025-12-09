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
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using MaterialClient.HttpHost;
using Microsoft.Extensions.Configuration;

namespace MaterialClient;

public partial class App : Application
{
    private IAbpApplicationWithInternalServiceProvider? _abpApplication;
    private WebApplication? _webApplication;

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

                // Start Web Host in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await StartWebHostAsync();
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


    private async Task StartWebHostAsync()
    {
        var builder = WebApplication.CreateBuilder();

        // Add ABP with HttpHost module
        await builder.AddApplicationAsync<MaterialClientHttpHostModule>();

        _webApplication = builder.Build();

        // Initialize ABP application
        await _webApplication.InitializeApplicationAsync();

        // Configure URLs (default to http://localhost:5000)
        var urls = builder.Configuration["Urls"] ?? "http://localhost:5000";
        _webApplication.Urls.Add(urls);

        System.Diagnostics.Debug.WriteLine($"Starting Web Host on {urls}");
        System.Diagnostics.Debug.WriteLine($"Swagger UI available at {urls}/swagger");

        // Start the web application
        await _webApplication.RunAsync();
    }

    private void OnApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        // Stop Web Host and ABP application synchronously
        Task.Run(async () =>
        {
            if (_webApplication != null)
            {
                System.Diagnostics.Debug.WriteLine("Stopping Web Host...");
                await _webApplication.StopAsync();
                await _webApplication.DisposeAsync();
            }

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