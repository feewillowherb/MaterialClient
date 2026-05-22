using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.UI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.Services;

/// <summary>
///     Polls hardware/services and builds the canonical DeviceStatusBar item list for any consumer app.
/// </summary>
public sealed class SharedDeviceStatusTracker : IDisposable, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly ISettingsService _settingsService;
    private readonly ILprDeviceOnlineStatusService _lprDeviceOnlineStatusService;
    private readonly ILogger<SharedDeviceStatusTracker> _logger;
    private readonly List<IDisposable> _timers = [];

    private DeviceStatusBarOptions _visibility = DeviceStatusBarOptions.CoreOnly;
    private bool _isScaleOnline;
    private bool _isCameraOnline;
    private bool _isUsbCameraOnline;
    private bool _isPrinterOnline;
    private bool _isLprOnline;
    private bool _isSoundDeviceOnline;
    private string _printerName = "";

    public SharedDeviceStatusTracker(
        IServiceProvider serviceProvider,
        ITruckScaleWeightService truckScaleWeightService,
        ISettingsService settingsService,
        ILprDeviceOnlineStatusService lprDeviceOnlineStatusService,
        ILogger<SharedDeviceStatusTracker> logger)
    {
        _serviceProvider = serviceProvider;
        _truckScaleWeightService = truckScaleWeightService;
        _settingsService = settingsService;
        _lprDeviceOnlineStatusService = lprDeviceOnlineStatusService;
        _logger = logger;
    }

    public event Action<DeviceStatusItem[]>? StatusesChanged;

    public async Task RefreshVisibilityFromSettingsAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            _visibility = DeviceStatusCatalog.FromSettings(
                settings.SystemSettings.DocumentCameraEnabled,
                settings.SystemSettings.EnablePrinter,
                settings.SoundDeviceSettings.Enabled);
            _printerName = settings.SystemSettings.SelectedPrinterName ?? string.Empty;

            if (!_visibility.DocumentCameraEnabled)
                _isUsbCameraOnline = false;
            if (!_visibility.PrinterEnabled)
                _isPrinterOnline = false;
            if (!_visibility.SoundDeviceEnabled)
                _isSoundDeviceOnline = false;

            NotifyChanged();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh device status bar visibility from settings");
        }
    }

    public void StartMonitoring()
    {
        _ = RefreshVisibilityFromSettingsAsync();

        _timers.Add(new Timer(_ =>
        {
            try
            {
                var isOnline = _truckScaleWeightService.IsOnline;
                UpdateScale(isOnline);
            }
            catch
            {
                UpdateScale(false);
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(2)));

        _ = CheckCameraStatusOnceAsync();

        _timers.Add(new Timer(_ =>
        {
            if (!_visibility.DocumentCameraEnabled) return;
            Task.Run(async () =>
            {
                try
                {
                    await CheckUsbCameraOnlineStatusAsync();
                }
                catch
                {
                    UpdateUsbCamera(false);
                }
            });
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5)));

        _timers.Add(new Timer(_ =>
        {
            if (!_visibility.PrinterEnabled) return;
            Task.Run(async () =>
            {
                try
                {
                    await CheckPrinterStatusOnceAsync();
                }
                catch
                {
                    UpdatePrinter(false);
                }
            });
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5)));

        _timers.Add(new Timer(_ =>
        {
            Task.Run(async () =>
            {
                try
                {
                    await CheckLprOnlineStatusAsync();
                }
                catch
                {
                    UpdateLpr(false);
                }
            });
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(30)));

        _timers.Add(new Timer(_ =>
        {
            if (!_visibility.SoundDeviceEnabled) return;
            Task.Run(async () =>
            {
                try
                {
                    var soundService = _serviceProvider.GetService<ISoundDeviceService>();
                    if (soundService == null)
                    {
                        UpdateSound(false);
                        return;
                    }

                    var isOnline = await soundService.IsOnlineAsync();
                    UpdateSound(isOnline);
                }
                catch
                {
                    UpdateSound(false);
                }
            });
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(8)));

        NotifyChanged();
    }

    public DeviceStatusItem[] GetCurrentStatuses() =>
        DeviceStatusCatalog.BuildItems(
            _visibility,
            _isScaleOnline,
            _isCameraOnline,
            _isUsbCameraOnline,
            _isPrinterOnline,
            _isLprOnline,
            _isSoundDeviceOnline);

    private void NotifyChanged() => StatusesChanged?.Invoke(GetCurrentStatuses());

    private void UpdateScale(bool online)
    {
        if (_isScaleOnline == online) return;
        _isScaleOnline = online;
        NotifyChanged();
    }

    private void UpdateCamera(bool online)
    {
        if (_isCameraOnline == online) return;
        _isCameraOnline = online;
        NotifyChanged();
    }

    private void UpdateUsbCamera(bool online)
    {
        if (_isUsbCameraOnline == online) return;
        _isUsbCameraOnline = online;
        NotifyChanged();
    }

    private void UpdatePrinter(bool online)
    {
        if (_isPrinterOnline == online) return;
        _isPrinterOnline = online;
        NotifyChanged();
    }

    private void UpdateLpr(bool online)
    {
        if (_isLprOnline == online) return;
        _isLprOnline = online;
        NotifyChanged();
    }

    private void UpdateSound(bool online)
    {
        if (_isSoundDeviceOnline == online) return;
        _isSoundDeviceOnline = online;
        NotifyChanged();
    }

    private async Task CheckPrinterStatusOnceAsync()
    {
        if (!_visibility.PrinterEnabled)
        {
            UpdatePrinter(false);
            return;
        }

        var printingService = _serviceProvider.GetService<ITicketPrintingService>();
        if (printingService == null || string.IsNullOrWhiteSpace(_printerName))
        {
            UpdatePrinter(false);
            return;
        }

        var installedPrinters = await Task.Run(() => printingService.ListInstalledPrinters());
        var isOnline = installedPrinters.Any(p =>
            string.Equals(p, _printerName, StringComparison.OrdinalIgnoreCase));
        UpdatePrinter(isOnline);
    }

    private async Task CheckUsbCameraOnlineStatusAsync()
    {
        if (!_visibility.DocumentCameraEnabled)
        {
            UpdateUsbCamera(false);
            return;
        }

        var usbCameraService = _serviceProvider.GetService<IUsbCameraService>();
        if (usbCameraService == null)
        {
            UpdateUsbCamera(false);
            return;
        }

        var isOnline = await usbCameraService.IsAvailableAsync();
        UpdateUsbCamera(isOnline);
    }

    private async Task CheckLprOnlineStatusAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        var deviceType = settings.SystemSettings.LprDeviceType;
        var configs = settings.LicensePlateRecognitionConfigs;

        if (configs == null || configs.Count == 0)
        {
            UpdateLpr(false);
            return;
        }

        var statuses = _lprDeviceOnlineStatusService.GetOnlineStatuses(deviceType, configs);
        UpdateLpr(statuses.Any(s => s.IsOnline));
    }

    private async Task CheckCameraStatusOnceAsync()
    {
        try
        {
            var hikvisionService = _serviceProvider.GetRequiredService<IHikvisionService>();
            var settings = await _settingsService.GetSettingsAsync();
            var cameraConfigs = settings.CameraConfigs;

            if (cameraConfigs.Count == 0)
            {
                UpdateCamera(false);
                return;
            }

            var anyOnline = false;
            foreach (var cameraConfig in cameraConfigs)
            {
                if (string.IsNullOrWhiteSpace(cameraConfig.Ip) ||
                    string.IsNullOrWhiteSpace(cameraConfig.Port) ||
                    string.IsNullOrWhiteSpace(cameraConfig.UserName) ||
                    string.IsNullOrWhiteSpace(cameraConfig.Password))
                {
                    continue;
                }

                if (!int.TryParse(cameraConfig.Port, out var port))
                {
                    continue;
                }

                var hikvisionConfig = new HikvisionDeviceConfig
                {
                    Ip = cameraConfig.Ip,
                    Port = port,
                    Username = cameraConfig.UserName,
                    Password = cameraConfig.Password,
                };

                if (await Task.Run(() => hikvisionService.IsOnline(hikvisionConfig)))
                {
                    anyOnline = true;
                    break;
                }
            }

            UpdateCamera(anyOnline);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check camera status");
            UpdateCamera(false);
        }
    }

    public void Dispose()
    {
        foreach (var timer in _timers)
        {
            timer.Dispose();
        }

        _timers.Clear();
    }
}
