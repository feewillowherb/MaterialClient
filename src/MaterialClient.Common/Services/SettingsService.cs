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
    ///     Get default delivery type from settings
    /// </summary>
    Task<DeliveryType> GetDefaultDeliveryTypeAsync();

    /// <summary>
    ///     Get the ProductCode derived from the current WeighingMode setting.
    ///     Standard -> ProductCode.Standard (5000), SolidWaste -> ProductCode.SolidWaste (5010), UrbanMode -> ProductCode.Urban (5030)
    /// </summary>
    Task<ProductCode> GetProductCodeAsync();

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
    private readonly IUrbanSettingsJsonStore _urbanSettingsJsonStore;
    private readonly IWindowsAutoStartService? _windowsAutoStartService;

    public SettingsService(
        IRepository<SettingsEntity, int> settingsRepository,
        IUnitOfWorkManager unitOfWorkManager,
        IUrbanSettingsJsonStore urbanSettingsJsonStore,
        IWindowsAutoStartService? windowsAutoStartService = null)
    {
        _settingsRepository = settingsRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _urbanSettingsJsonStore = urbanSettingsJsonStore;
        _windowsAutoStartService = windowsAutoStartService;
    }

    /// <summary>
    ///     Get current settings
    /// </summary>
    public async Task<SettingsEntity> GetSettingsAsync()
    {
        SettingsEntity settings;
        using (var uow = _unitOfWorkManager.Begin())
        {
            var loaded = await _settingsRepository.GetListAsync();
            var existing = loaded.FirstOrDefault();
            if (existing == null)
            {
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
            else
            {
                settings = existing;
            }
        }

        var urbanJson = await _urbanSettingsJsonStore.GetJsonAsync();
        if (urbanJson != null)
            settings.UrbanSettingsJson = urbanJson;

        return settings;
    }

    /// <summary>
    ///     Save settings
    /// </summary>
    public async Task SaveSettingsAsync(SettingsEntity settings)
    {
        using (var uow = _unitOfWorkManager.Begin())
        {
            var existingSettings = await _settingsRepository.GetListAsync();
            var existing = existingSettings.FirstOrDefault();

            if (existing != null)
            {
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
                await _settingsRepository.InsertAsync(settings);
            }

            await uow.CompleteAsync();
        }

        // Urban 与内核是同一 SQLite 上的另一 DbContext；必须在内核事务结束后再写，否则 SQLITE_BUSY。
        await _urbanSettingsJsonStore.SaveJsonAsync(settings.UrbanSettingsJson);

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
    ///     Get default delivery type from settings
    /// </summary>
    public async Task<DeliveryType> GetDefaultDeliveryTypeAsync()
    {
        var settings = await GetSettingsAsync();
        return settings.SystemSettings.DefaultDeliveryType;
    }

    /// <summary>
    ///     Get the ProductCode derived from the current WeighingMode setting.
    /// </summary>
    public async Task<ProductCode> GetProductCodeAsync()
    {
        var weighingMode = await GetWeighingModeAsync();
        return weighingMode switch
        {
            WeighingMode.SolidWaste => ProductCode.SolidWaste,
            WeighingMode.UrbanMode => ProductCode.Urban,
            WeighingMode.Recycle => ProductCode.Recycle,
            _ => ProductCode.Standard
        };
    }

    /// <summary>
    ///     Persist default weighing mode from the given product code (e.g. after successful auth).
    /// </summary>
    public async Task SaveDefaultWeighingModeAsync(ProductCode productCode)
    {
        var settings = await GetSettingsAsync();
        var systemSettings = settings.SystemSettings;
        systemSettings.DefaultWeighingMode = productCode switch
        {
            ProductCode.SolidWaste => WeighingMode.SolidWaste,
            ProductCode.Urban => WeighingMode.UrbanMode,
            ProductCode.Recycle => WeighingMode.Recycle,
            _ => WeighingMode.Standard
        };
        settings.SystemSettings = systemSettings;
        await SaveSettingsAsync(settings);
    }
}