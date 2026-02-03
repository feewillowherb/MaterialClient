using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

/// <summary>
///     Settings service interface
/// </summary>
public interface ISettingsService
{
    /// <summary>
    ///     Get current settings
    /// </summary>
    Task<SettingsEntity> GetSettingsAsync();

    /// <summary>
    ///     Save settings
    /// </summary>
    Task SaveSettingsAsync(SettingsEntity settings);

    /// <summary>
    ///     Get default weighing mode from settings
    /// </summary>
    Task<WeighingMode> GetWeighingModeAsync();

    /// <summary>
    ///     Persist default weighing mode from the given product code (e.g. after successful auth).
    /// </summary>
    /// <param name="productCode">Product code chosen for authorization</param>
    Task SaveDefaultWeighingModeAsync(ProductCode productCode);
}

/// <summary>
///     Settings service implementation
/// </summary>
public class SettingsService : DomainService, ISettingsService
{
    private readonly IRepository<SettingsEntity, int> _settingsRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IWindowsAutoStartService? _windowsAutoStartService;

    public SettingsService(
        IRepository<SettingsEntity, int> settingsRepository,
        IUnitOfWorkManager unitOfWorkManager,
        IWindowsAutoStartService? windowsAutoStartService = null)
    {
        _settingsRepository = settingsRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _windowsAutoStartService = windowsAutoStartService;
    }

    /// <summary>
    ///     Get current settings
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
                new List<LicensePlateRecognitionConfig>(),
                new WeighingConfiguration(),
                new SoundDeviceSettings());

            await _settingsRepository.InsertAsync(settings);
            await uow.CompleteAsync();
        }

        return settings;
    }

    /// <summary>
    ///     Save settings
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
            existing.WeighingConfiguration = settings.WeighingConfiguration;
            existing.SoundDeviceSettings = settings.SoundDeviceSettings;

            await _settingsRepository.UpdateAsync(existing);
        }
        else
        {
            // Insert new settings
            await _settingsRepository.InsertAsync(settings);
        }

        await uow.CompleteAsync();

        // Synchronize Windows auto-start registry with database setting
        if (_windowsAutoStartService != null)
        {
            try
            {
                if (settings.SystemSettings.EnableAutoStart)
                {
                    await _windowsAutoStartService.EnableAutoStartAsync();
                }
                else
                {
                    await _windowsAutoStartService.DisableAutoStartAsync();
                }
            }
            catch (Exception ex)
            {
                // Log warning but don't fail the save operation
                // Registry sync failures should not prevent settings from being saved
                Logger.LogWarning(ex, "Failed to synchronize auto-start setting with Windows registry");
            }
        }

        // TODO: Restart all devices after saving settings
        // - Restart truck scale service with new scale settings
        // - Restart camera services with new camera configurations
        // - Restart document scanner service with new USB device
        // - Restart license plate recognition services with new configurations
    }

    /// <summary>
    ///     Get default weighing mode from settings
    /// </summary>
    public async Task<WeighingMode> GetWeighingModeAsync()
    {
        var settings = await GetSettingsAsync();
        return settings.SystemSettings.DefaultWeighingMode;
    }

    /// <summary>
    ///     Persist default weighing mode from the given product code (e.g. after successful auth).
    /// </summary>
    [UnitOfWork]
    public async Task SaveDefaultWeighingModeAsync(ProductCode productCode)
    {
        var settings = await GetSettingsAsync();
        var systemSettings = settings.SystemSettings;
        systemSettings.DefaultWeighingMode = productCode == ProductCode.SolidWaste ? WeighingMode.SolidWaste : WeighingMode.Standard;
        settings.SystemSettings = systemSettings;
        await SaveSettingsAsync(settings);
    }
}