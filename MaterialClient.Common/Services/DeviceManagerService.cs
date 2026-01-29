using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Services;

namespace MaterialClient.Common.Services;

/// <summary>
///     Device manager service interface
/// </summary>
public interface IDeviceManagerService
{
    /// <summary>
    ///     Start all devices
    /// </summary>
    Task StartAsync();

    /// <summary>
    ///     Close all devices
    /// </summary>
    Task CloseAsync();

    /// <summary>
    ///     Restart all devices
    /// </summary>
    Task RestartAsync();
}

/// <summary>
///     Device manager service implementation
/// </summary>
[AutoConstructor]
public partial class DeviceManagerService : DomainService, IDeviceManagerService
{
    private readonly ILogger<DeviceManagerService>? _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsService _settingsService;
    private bool _isStarted = false; // 启动状态标志

    /// <summary>
    ///     Start all devices
    /// </summary>
    public async Task StartAsync()
    {
        // 如果已经启动，直接返回
        if (_isStarted)
        {
            _logger?.LogDebug("设备已经启动，跳过重复启动");
            return;
        }

        try
        {
            // Start truck scale service
            var settings = await _settingsService.GetSettingsAsync();
            var truckScaleService = GetTruckScaleWeightService();
            var initialized = await truckScaleService.InitializeAsync(settings.ScaleSettings);
            if (initialized)
                _logger?.LogInformation("Truck scale service started successfully");
            else
                _logger?.LogWarning("Failed to start truck scale service");

            // Start Hikvision camera services
            await StartHikvisionCamerasAsync(settings);

            // Start Hikvision LPR service if device type is Hikvision
            if (settings.SystemSettings.LprDeviceType == LprDeviceType.Hikvision)
            {
                await StartHikvisionLprServiceAsync();
            }

            _isStarted = true; // 标记为已启动

            // TODO: Start other devices
            // - Start document scanner service
            // - Start other license plate recognition services (LprAllInOne, Huaxiazhixin)
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error starting devices");
            throw;
        }
    }

    /// <summary>
    ///     Close all devices
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

            // Stop Hikvision LPR service if it was started
            await StopHikvisionLprServiceAsync();

            _isStarted = false; // 重置启动状态

            // TODO: Close other devices
            // - Close document scanner service
            // - Close other license plate recognition services

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error closing devices");
            throw;
        }
    }

    /// <summary>
    ///     Restart all devices
    /// </summary>
    public async Task RestartAsync()
    {
        try
        {
            // 先关闭设备
            if (_isStarted)
            {
                await CloseAsync();
            }

            // 重置状态并重新启动
            _isStarted = false;
            await StartAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error restarting devices");
            throw;
        }
    }

    /// <summary>
    ///     Get truck scale weight service lazily to avoid circular dependency
    /// </summary>
    private ITruckScaleWeightService GetTruckScaleWeightService()
    {
        return _serviceProvider.GetRequiredService<ITruckScaleWeightService>();
    }

    /// <summary>
    ///     Get Hikvision service lazily to avoid circular dependency
    /// </summary>
    private IHikvisionService GetHikvisionService()
    {
        return _serviceProvider.GetRequiredService<IHikvisionService>();
    }

    /// <summary>
    ///     Get Hikvision LPR service lazily to avoid circular dependency
    /// </summary>
    private IHikvisionLprService GetHikvisionLprService()
    {
        return _serviceProvider.GetRequiredService<IHikvisionLprService>();
    }

    /// <summary>
    ///     Start Hikvision cameras (login and verify)
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

            var successCount = 0;
            var failCount = 0;

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
                    _logger?.LogWarning(
                        $"Hikvision camera '{cameraConfig.Name}' has invalid port: {cameraConfig.Port}");
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
                    _logger?.LogInformation(
                        $"Hikvision camera '{cameraConfig.Name}' ({cameraConfig.Ip}:{port}) started successfully");
                    successCount++;
                }
                else
                {
                    _logger?.LogWarning(
                        $"Hikvision camera '{cameraConfig.Name}' ({cameraConfig.Ip}:{port}) failed to login");
                    failCount++;
                }
            }

            if (successCount > 0)
                _logger?.LogInformation($"Hikvision cameras: {successCount} online, {failCount} offline");
            else if (failCount > 0) _logger?.LogWarning($"All Hikvision cameras failed to start ({failCount} cameras)");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error starting Hikvision cameras");
            // Don't throw, allow other devices to continue starting
        }
    }

    /// <summary>
    ///     Start Hikvision LPR service
    /// </summary>
    private async Task StartHikvisionLprServiceAsync()
    {
        try
        {
            var hikvisionLprService = GetHikvisionLprService();
            
            // 添加配置的 LPR 设备
            var settings = await _settingsService.GetSettingsAsync();
            var lprConfigs = settings.LicensePlateRecognitionConfigs;
            
            if (lprConfigs == null || lprConfigs.Count == 0)
            {
                _logger?.LogInformation("No Hikvision LPR devices configured");
            }
            else
            {
                foreach (var lprConfig in lprConfigs)
                {
                    if (lprConfig.IsValid())
                    {
                        hikvisionLprService.AddOrUpdateDevice(lprConfig);
                        _logger?.LogInformation("Hikvision LPR device added: {Name} ({Ip})", lprConfig.Name, lprConfig.Ip);
                    }
                    else
                    {
                        _logger?.LogWarning("Hikvision LPR device configuration invalid: {Name} ({Ip})", lprConfig.Name, lprConfig.Ip);
                    }
                }
            }

            // 启动监听服务
            var started = await hikvisionLprService.StartAsync();
            if (started)
            {
                _logger?.LogInformation("Hikvision LPR service started successfully");
            }
            else
            {
                _logger?.LogWarning("Hikvision LPR service failed to start or was already started");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error starting Hikvision LPR service");
            // Don't throw, allow other devices to continue starting
        }
    }

    /// <summary>
    ///     Stop Hikvision LPR service
    /// </summary>
    private async Task StopHikvisionLprServiceAsync()
    {
        try
        {
            var hikvisionLprService = GetHikvisionLprService();
            await hikvisionLprService.StopAsync();
            _logger?.LogInformation("Hikvision LPR service stopped");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping Hikvision LPR service");
            // Don't throw, allow other cleanup to continue
        }
    }
}