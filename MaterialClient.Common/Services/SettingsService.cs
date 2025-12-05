using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
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
}

/// <summary>
/// Settings service implementation
/// </summary>
public class SettingsService : DomainService, ISettingsService
{
    private readonly IRepository<SettingsEntity, int> _settingsRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public SettingsService(
        IRepository<SettingsEntity, int> settingsRepository,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _settingsRepository = settingsRepository;
        _unitOfWorkManager = unitOfWorkManager;
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
}