using Avalonia;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Volo.Abp;
using Volo.Abp.Autofac;
using MaterialClient.HttpHost;
using MaterialClient.EFCore;
using MaterialClient.Services;
using Microsoft.EntityFrameworkCore;

namespace MaterialClient;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Start HTTP Host in background thread
        Task.Run(() => StartHttpHostAsync());
        
        // Start Avalonia UI
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static IAbpApplication? _abpApplication;

    private static async Task StartHttpHostAsync()
    {
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = Array.Empty<string>(),
                ContentRootPath = AppContext.BaseDirectory
            });

            // Configure SQLite connection
            var connectionString = builder.Configuration.GetConnectionString("Default") 
                ?? "Data Source=MaterialClient.db";
            
            // Configure DbContext in services (connection string will be used in MaterialClientCommonModule)
            builder.Configuration["ConnectionStrings:Default"] = connectionString;

            // Configure ABP Application with Autofac
            _abpApplication = await builder.Services.AddApplicationAsync<MaterialClientHttpHostModule>(options =>
            {
                options.UseAutofac();
                options.Services.ReplaceConfiguration(builder.Configuration);
            });

            var app = builder.Build();
            
            // Initialize ABP
            await app.InitializeApplicationAsync();

            // Initialize ServiceLocator for Avalonia UI layer to access ABP services
            var serviceProvider = app.Services;
            ServiceLocator.Initialize(serviceProvider);

            // Configure HTTP server URL
            app.Urls.Add("http://localhost:5000");

            // Run HTTP server
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            // Log error but don't crash the application
            System.Diagnostics.Debug.WriteLine($"HTTP Host startup failed: {ex.Message}");
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}