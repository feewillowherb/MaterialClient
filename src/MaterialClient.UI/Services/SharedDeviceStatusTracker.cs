using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.UI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

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
    private readonly ILocalEventBus _localEventBus;
    private readonly ISharedDeviceStatusTrackerRegistry _trackerRegistry;
    private readonly ILogger<SharedDeviceStatusTracker> _logger;
    private readonly List<IDisposable> _timers = [];
    private bool _isMonitoring;

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
        ILocalEventBus localEventBus,
        ISharedDeviceStatusTrackerRegistry trackerRegistry,
        ILogger<SharedDeviceStatusTracker> logger)
    {
        _serviceProvider = serviceProvider;
        _truckScaleWeightService = truckScaleWeightService;
        _settingsService = settingsService;
        _lprDeviceOnlineStatusService = lprDeviceOnlineStatusService;
        _localEventBus = localEventBus;
        _trackerRegistry = trackerRegistry;
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
                UpdateUsbCamera(false);
            if (!_visibility.PrinterEnabled)
                UpdatePrinter(false);
            if (!_visibility.SoundDeviceEnabled)
                UpdateSound(false);

            NotifyChanged();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh device status bar visibility from settings");
        }
    }

    public void StartMonitoring()
    {
        if (_isMonitoring) return;

        _isMonitoring = true;
        _trackerRegistry.Register(this);

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

    /// <summary>
    ///     Publishes the current online/offline state for all server-mapped devices.
    ///     Used after SignalR reconnect to refresh server-side cache immediately.
    /// </summary>
    public void RepublishCurrentStatuses()
    {
        PublishDeviceStatusChanged(DeviceStatusCatalog.ScaleName, _isScaleOnline);
        PublishDeviceStatusChanged(DeviceStatusCatalog.CameraName, _isCameraOnline);
        PublishDeviceStatusChanged(DeviceStatusCatalog.LprName, _isLprOnline);

        if (_visibility.DocumentCameraEnabled)
        {
            PublishDeviceStatusChanged(DeviceStatusCatalog.UsbCameraName, _isUsbCameraOnline);
        }

        if (_visibility.PrinterEnabled)
        {
            PublishDeviceStatusChanged(DeviceStatusCatalog.PrinterName, _isPrinterOnline);
        }

        if (_visibility.SoundDeviceEnabled)
        {
            PublishDeviceStatusChanged(DeviceStatusCatalog.SoundDeviceName, _isSoundDeviceOnline);
        }
    }

    private void NotifyChanged() => StatusesChanged?.Invoke(GetCurrentStatuses());

    private void UpdateScale(bool online) => UpdateDeviceOnline(DeviceStatusCatalog.ScaleName, online, v => _isScaleOnline = v, () => _isScaleOnline);

    private void UpdateCamera(bool online) => UpdateDeviceOnline(DeviceStatusCatalog.CameraName, online, v => _isCameraOnline = v, () => _isCameraOnline);

    private void UpdateUsbCamera(bool online) => UpdateDeviceOnline(DeviceStatusCatalog.UsbCameraName, online, v => _isUsbCameraOnline = v, () => _isUsbCameraOnline);

    private void UpdatePrinter(bool online) => UpdateDeviceOnline(DeviceStatusCatalog.PrinterName, online, v => _isPrinterOnline = v, () => _isPrinterOnline);

    private void UpdateLpr(bool online) => UpdateDeviceOnline(DeviceStatusCatalog.LprName, online, v => _isLprOnline = v, () => _isLprOnline);

    private void UpdateSound(bool online) => UpdateDeviceOnline(DeviceStatusCatalog.SoundDeviceName, online, v => _isSoundDeviceOnline = v, () => _isSoundDeviceOnline);

    private void UpdateDeviceOnline(string displayName, bool online, Action<bool> setOnline, Func<bool> getOnline)
    {
        if (getOnline() == online) return;
        setOnline(online);
        PublishDeviceStatusChanged(displayName, online);
        NotifyChanged();
    }

    private void PublishDeviceStatusChanged(string displayName, bool isOnline)
    {
        if (!DeviceStatusCatalog.TryMapToServerDeviceType(displayName, out var deviceType))
        {
            return;
        }

        var status = isOnline ? "Online" : "Offline";
        _ = _localEventBus.PublishAsync(new DeviceStatusChangedEventData(deviceType, status));
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
        _trackerRegistry.Unregister(this);
        _isMonitoring = false;

        foreach (var timer in _timers)
        {
            timer.Dispose();
        }

        _timers.Clear();
    }
}
