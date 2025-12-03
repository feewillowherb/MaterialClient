using System;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Services.Hardware;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

/// <summary>
/// Settings service interface
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Get current settings
    /// </summary>
    Task<SettingsEntity> GetSettingsAsync();

    /// <summary>
    /// Save settings
    /// </summary>
    Task SaveSettingsAsync(SettingsEntity settings);

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
/// Settings service implementation
/// </summary>
public class SettingsService : DomainService, ISettingsService
{
    private readonly IRepository<SettingsEntity, int> _settingsRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly ILogger<SettingsService>? _logger;

    public SettingsService(
        IRepository<SettingsEntity, int> settingsRepository,
        IUnitOfWorkManager unitOfWorkManager,
        ITruckScaleWeightService truckScaleWeightService,
        ILogger<SettingsService>? logger = null)
    {
        _settingsRepository = settingsRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _truckScaleWeightService = truckScaleWeightService;
        _logger = logger;
    }

    /// <summary>
    /// Get current settings
    /// </summary>
    public async Task<SettingsEntity> GetSettingsAsync()
    {
        using var uow = _unitOfWorkManager.Begin();

        var settingsList = await _settingsRepository.GetListAsync();
        var settings = settingsList.FirstOrDefault();

        if (settings == null)
        {
            // Create default settings if none exist
            settings = new SettingsEntity(
                new ScaleSettings(),
                new DocumentScannerConfig(),
                new SystemSettings(),
                new List<CameraConfig>(),
                new List<LicensePlateRecognitionConfig>());

            await _settingsRepository.InsertAsync(settings);
            await uow.CompleteAsync();
        }

        return settings;
    }

    /// <summary>
    /// Save settings
    /// </summary>
    [UnitOfWork]
    public async Task SaveSettingsAsync(SettingsEntity settings)
    {
        using var uow = _unitOfWorkManager.Begin();

        var existingSettings = await _settingsRepository.GetListAsync();
        var existing = existingSettings.FirstOrDefault();

        if (existing != null)
        {
            // Update existing settings
            existing.ScaleSettings = settings.ScaleSettings;
            existing.DocumentScannerConfig = settings.DocumentScannerConfig;
            existing.SystemSettings = settings.SystemSettings;
            existing.CameraConfigs = settings.CameraConfigs;
            existing.LicensePlateRecognitionConfigs = settings.LicensePlateRecognitionConfigs;

            await _settingsRepository.UpdateAsync(existing);
        }
        else
        {
            // Insert new settings
            await _settingsRepository.InsertAsync(settings);
        }

        await uow.CompleteAsync();

        // TODO: Restart all devices after saving settings
        // - Restart truck scale service with new scale settings
        // - Restart camera services with new camera configurations
        // - Restart document scanner service with new USB device
        // - Restart license plate recognition services with new configurations
    }

    /// <summary>
    /// Start all devices
    /// </summary>
    public async Task StartAsync()
    {
        try
        {
            // Start truck scale service
            var settings = await GetSettingsAsync();
            var initialized = await _truckScaleWeightService.InitializeAsync(settings.ScaleSettings);
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
            _truckScaleWeightService.Close();
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
            var restarted = await _truckScaleWeightService.RestartAsync();
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