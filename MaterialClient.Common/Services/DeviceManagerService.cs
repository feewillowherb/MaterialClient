using System;
using System.Threading.Tasks;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Services.Hardware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Services;

namespace MaterialClient.Common.Services;

/// <summary>
/// Device manager service interface
/// </summary>
public interface IDeviceManagerService
{
    /// <summary>
    /// Start all devices
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Close all devices
    /// </summary>
    Task CloseAsync();

    /// <summary>
    /// Restart all devices
    /// </summary>
    Task RestartAsync();
}

/// <summary>
/// Device manager service implementation
/// </summary>
public class DeviceManagerService : DomainService, IDeviceManagerService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<DeviceManagerService>? _logger;

    public DeviceManagerService(
        IServiceProvider serviceProvider,
        ISettingsService settingsService,
        ILogger<DeviceManagerService>? logger = null)
    {
        _serviceProvider = serviceProvider;
        _settingsService = settingsService;
        _logger = logger;
    }
    
    /// <summary>
    /// Get truck scale weight service lazily to avoid circular dependency
    /// </summary>
    private ITruckScaleWeightService GetTruckScaleWeightService()
    {
        return _serviceProvider.GetRequiredService<ITruckScaleWeightService>();
    }

    /// <summary>
    /// Start all devices
    /// </summary>
    public async Task StartAsync()
    {
        try
        {
            // Start truck scale service
            var settings = await _settingsService.GetSettingsAsync();
            var truckScaleService = GetTruckScaleWeightService();
            var initialized = await truckScaleService.InitializeAsync(settings.ScaleSettings);
            if (initialized)
            {
                _logger?.LogInformation("Truck scale service started successfully");
            }
            else
            {
                _logger?.LogWarning("Failed to start truck scale service");
            }

            // TODO: Start other devices
            // - Start camera services
            // - Start document scanner service
            // - Start license plate recognition services
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error starting devices");
            throw;
        }
    }

    /// <summary>
    /// Close all devices
    /// </summary>
    public async Task CloseAsync()
    {
        try
        {
            // Close truck scale service
            var truckScaleService = GetTruckScaleWeightService();
            truckScaleService.Close();
            _logger?.LogInformation("Truck scale service closed");

            // TODO: Close other devices
            // - Close camera services
            // - Close document scanner service
            // - Close license plate recognition services

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error closing devices");
            throw;
        }
    }

    /// <summary>
    /// Restart all devices
    /// </summary>
    public async Task RestartAsync()
    {
        try
        {
            // Restart truck scale service
            var truckScaleService = GetTruckScaleWeightService();
            var restarted = await truckScaleService.RestartAsync();
            if (restarted)
            {
                _logger?.LogInformation("Truck scale service restarted successfully");
            }
            else
            {
                _logger?.LogWarning("Failed to restart truck scale service");
            }

            // TODO: Restart other devices
            // - Restart camera services
            // - Restart document scanner service
            // - Restart license plate recognition services
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error restarting devices");
            throw;
        }
    }
}
