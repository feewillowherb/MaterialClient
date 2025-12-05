using System;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
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
[AutoConstructor]
public partial class DeviceManagerService : DomainService, IDeviceManagerService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<DeviceManagerService>? _logger;
    
    /// <summary>
    /// Get truck scale weight service lazily to avoid circular dependency
    /// </summary>
    private ITruckScaleWeightService GetTruckScaleWeightService()
    {
        return _serviceProvider.GetRequiredService<ITruckScaleWeightService>();
    }

    /// <summary>
    /// Get Hikvision service lazily to avoid circular dependency
    /// </summary>
    private IHikvisionService GetHikvisionService()
    {
        return _serviceProvider.GetRequiredService<IHikvisionService>();
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

            // Start Hikvision camera services
            await StartHikvisionCamerasAsync(settings);

            // TODO: Start other devices
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

            // Close Hikvision camera services
            // Note: HikvisionService uses login/logout per operation, so no explicit cleanup needed
            _logger?.LogInformation("Hikvision camera services closed");

            // TODO: Close other devices
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

            // Restart Hikvision camera services
            var settings = await _settingsService.GetSettingsAsync();
            await StartHikvisionCamerasAsync(settings);

            // TODO: Restart other devices
            // - Restart document scanner service
            // - Restart license plate recognition services
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error restarting devices");
            throw;
        }
    }

    /// <summary>
    /// Start Hikvision cameras (login and verify)
    /// </summary>
    private async Task StartHikvisionCamerasAsync(SettingsEntity settings)
    {
        try
        {
            var hikvisionService = GetHikvisionService();
            var cameraConfigs = settings.CameraConfigs;

            if (cameraConfigs == null || cameraConfigs.Count == 0)
            {
                _logger?.LogInformation("No Hikvision cameras configured");
                return;
            }

            int successCount = 0;
            int failCount = 0;

            foreach (var cameraConfig in cameraConfigs)
            {
                if (string.IsNullOrWhiteSpace(cameraConfig.Ip) ||
                    string.IsNullOrWhiteSpace(cameraConfig.Port) ||
                    string.IsNullOrWhiteSpace(cameraConfig.UserName) ||
                    string.IsNullOrWhiteSpace(cameraConfig.Password))
                {
                    _logger?.LogWarning($"Hikvision camera '{cameraConfig.Name}' has incomplete configuration");
                    failCount++;
                    continue;
                }

                if (!int.TryParse(cameraConfig.Port, out var port))
                {
                    _logger?.LogWarning($"Hikvision camera '{cameraConfig.Name}' has invalid port: {cameraConfig.Port}");
                    failCount++;
                    continue;
                }

                var hikvisionConfig = new HikvisionDeviceConfig
                {
                    Ip = cameraConfig.Ip,
                    Port = port,
                    Username = cameraConfig.UserName,
                    Password = cameraConfig.Password
                };

                // Add device to HikvisionService
                hikvisionService.AddOrUpdateDevice(hikvisionConfig);

                // Verify camera is online (login test)
                var isOnline = await Task.Run(() => hikvisionService.IsOnline(hikvisionConfig));
                if (isOnline)
                {
                    _logger?.LogInformation($"Hikvision camera '{cameraConfig.Name}' ({cameraConfig.Ip}:{port}) started successfully");
                    successCount++;
                }
                else
                {
                    _logger?.LogWarning($"Hikvision camera '{cameraConfig.Name}' ({cameraConfig.Ip}:{port}) failed to login");
                    failCount++;
                }
            }

            if (successCount > 0)
            {
                _logger?.LogInformation($"Hikvision cameras: {successCount} online, {failCount} offline");
            }
            else if (failCount > 0)
            {
                _logger?.LogWarning($"All Hikvision cameras failed to start ({failCount} cameras)");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error starting Hikvision cameras");
            // Don't throw, allow other devices to continue starting
        }
    }
}
