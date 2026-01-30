using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Views.Dialogs;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.ViewModels;

/// <summary>
///     Settings window ViewModel
/// </summary>
public partial class SettingsWindowViewModel : ViewModelBase, ITransientDependency
{
    private readonly ISettingsService _settingsService;
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly IHikvisionService _hikvisionService;
    private readonly ITicketPrintingService _ticketPrintingService;
    private readonly ILogger<SettingsWindowViewModel> _logger;

    [Reactive] private ObservableCollection<string> _availableSerialPorts = new();

    // Camera configs
    [Reactive] private ObservableCollection<CameraConfigViewModel> _cameraConfigs = new();

    // Document scanner settings
    [Reactive] private string? _documentScannerUsbDevice;

    // System settings
    [Reactive] private bool _enableAutoStart;
    [Reactive] private StreamType _captureStreamType = StreamType.Substream;
    [Reactive] private string _urls = "http://localhost:9960";
    [Reactive] private LprDeviceType _lprDeviceType = LprDeviceType.Hikvision;
    [Reactive] private bool _enablePrinter;
    [Reactive] private string _selectedPrinterName = string.Empty;

    // License plate recognition configs
    [Reactive] private ObservableCollection<LicensePlateRecognitionConfigViewModel> _licensePlateRecognitionConfigs =
        new();

    [Reactive] private string _scaleBaudRate = "9600";

    [Reactive] private string _scaleCommunicationMethod = "TF0";

    // Scale settings
    [Reactive] private string _scaleSerialPort = "COM3";
    [Reactive] private ScaleUnit _scaleUnit = ScaleUnit.Ton;
    [Reactive] private ScaleType _scaleType = ScaleType.Yaohua;

    /// <summary>
    ///     Scale unit options for ComboBox
    /// </summary>
    public ObservableCollection<ScaleUnit> ScaleUnitOptions { get; } = new()
    {
        ScaleUnit.Kg,
        ScaleUnit.Ton,
        ScaleUnit.TenGram,
        ScaleUnit.HundredGram,
        ScaleUnit.Gram
    };

    /// <summary>
    ///     Scale type options for ComboBox
    /// </summary>
    public ObservableCollection<ScaleType> ScaleTypeOptions { get; } = new()
    {
        ScaleType.Yaohua,
        ScaleType.DingSong
    };

    /// <summary>
    ///     Stream type options for ComboBox
    /// </summary>
    public ObservableCollection<StreamType> StreamTypeOptions { get; } = new()
    {
        StreamType.Substream,
        StreamType.Mainstream
    };

    /// <summary>
    ///     车牌识别设备类型选项（用于下拉框）
    /// </summary>
    public ObservableCollection<LprDeviceType> LprDeviceTypeOptions { get; } = new()
    {
        LprDeviceType.Hikvision,
        LprDeviceType.LprAllInOne,
        LprDeviceType.Huaxiazhixin
    };

    /// <summary>
    ///     是否显示海康威视专用配置字段
    /// </summary>
    public bool ShowHikvisionLprFields => LprDeviceType == LprDeviceType.Hikvision;

    // Weighing configuration
    [Reactive] private decimal _minWeightThreshold = 0.5m;
    [Reactive] private decimal _weightStabilityThreshold = 0.05m;
    [Reactive] private int _stabilityWindowMs = 3000;
    [Reactive] private int _stabilityCheckIntervalMs = 200;
    [Reactive] private int _maxIntervalMinutes = 300;
    [Reactive] private decimal _minWeightDiff = 1m;

    // Sound device settings
    [Reactive] private bool _soundDeviceEnabled = false;
    [Reactive] private string _soundDeviceLocalIP = string.Empty;
    [Reactive] private string _soundDeviceSoundIP = string.Empty;
    [Reactive] private string _soundDeviceSoundSN = string.Empty;
    [Reactive] private string _soundDeviceSoundVolume = "0";

    public ObservableCollection<string> AvailablePrinters { get; } = new();

    public SettingsWindowViewModel(
        ISettingsService settingsService,
        ITruckScaleWeightService truckScaleWeightService,
        IHikvisionService hikvisionService,
        ITicketPrintingService ticketPrintingService,
        ILogger<SettingsWindowViewModel> logger)
    {
        _settingsService = settingsService;
        _truckScaleWeightService = truckScaleWeightService;
        _hikvisionService = hikvisionService;
        _ticketPrintingService = ticketPrintingService;
        _logger = logger;

        // Subscribe to LprDeviceType changes to notify ShowHikvisionLprFields property change
        this.WhenAnyValue(x => x.LprDeviceType)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ShowHikvisionLprFields)));

        // Load available serial ports
        RefreshAvailableSerialPorts();
        RefreshAvailablePrinters();

        // Load settings
        _ = LoadSettingsAsync();
    }

    #region Events

    /// <summary>
    ///     Event raised when the window should be closed
    /// </summary>
    public event EventHandler? CloseRequested;

    #endregion

    #region Commands

    [ReactiveCommand]
    private async Task SaveAsync()
    {
        try
        {
            // Preserve non-UI SystemSettings fields (e.g. DefaultWeighingMode) to avoid losing state.
            var existingSettings = await _settingsService.GetSettingsAsync();
            var systemSettings = existingSettings.SystemSettings;
            systemSettings.EnableAutoStart = EnableAutoStart;
            systemSettings.CaptureStreamType = CaptureStreamType;
            systemSettings.Urls = Urls;
            systemSettings.LprDeviceType = LprDeviceType;
            systemSettings.EnablePrinter = EnablePrinter;
            systemSettings.SelectedPrinterName = SelectedPrinterName;

            var settings = new SettingsEntity(
                new ScaleSettings
                {
                    SerialPort = ScaleSerialPort,
                    BaudRate = ScaleBaudRate,
                    CommunicationMethod = ScaleCommunicationMethod,
                    ScaleUnit = ScaleUnit,
                    ScaleType = ScaleType
                },
                new DocumentScannerConfig
                {
                    UsbDevice = DocumentScannerUsbDevice
                },
                systemSettings,
                CameraConfigs.Select(c => new CameraConfig
                {
                    Name = c.Name,
                    Ip = c.Ip,
                    Port = c.Port,
                    Channel = c.Channel,
                    UserName = c.UserName,
                    Password = c.Password
                }).ToList(),
                LicensePlateRecognitionConfigs.Select(l =>
                {
                    var config = new LicensePlateRecognitionConfig
                    {
                        Name = l.Name,
                        Ip = l.Ip,
                        Direction = l.Direction,
                        UserName = l.UserName,
                        Password = l.Password,
                        Port = l.Port,
                        Channel = l.Channel
                    };
                    if (HikvisionLprDefaults.ShouldApply(LprDeviceType))
                        HikvisionLprDefaults.ApplyDefaults(config);
                    return config;
                }).ToList(),
                new WeighingConfiguration
                {
                    MinWeightThreshold = MinWeightThreshold,
                    WeightStabilityThreshold = WeightStabilityThreshold,
                    StabilityWindowMs = StabilityWindowMs,
                    StabilityCheckIntervalMs = StabilityCheckIntervalMs,
                    MaxIntervalMinutes = MaxIntervalMinutes,
                    MinWeightDiff = MinWeightDiff
                },
                new SoundDeviceSettings
                {
                    Enabled = SoundDeviceEnabled,
                    LocalIP = SoundDeviceLocalIP,
                    SoundIP = SoundDeviceSoundIP,
                    SoundSN = SoundDeviceSoundSN,
                    SoundVolume = SoundDeviceSoundVolume
                }
            );

            await _settingsService.SaveSettingsAsync(settings);

            // Restart truck scale service with new settings
            await _truckScaleWeightService.RestartAsync();

            // Notify that settings have been saved
            MessageBus.Current.SendMessage(new SettingsSavedMessage());

            // Close window after saving
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Handle error
        }
    }

    [ReactiveCommand]
    private void Cancel()
    {
        // Raise close requested event
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [ReactiveCommand]
    private async Task AddCameraAsync()
    {
        var dialogViewModel = new AddCameraDialogViewModel
        {
            Name = $"camera_{CameraConfigs.Count + 1}",
            Port = "8000",
            Channel = "1",
            UserName = "admin"
        };

        var dialog = new AddCameraDialog(dialogViewModel);
        var window = GetWindow();
        var result = await dialog.ShowDialog<CameraConfigViewModel?>(window);

        if (result != null)
        {
            CameraConfigs.Add(result);
        }
    }

    [ReactiveCommand]
    private void RemoveCamera(CameraConfigViewModel? config)
    {
        if (config != null) CameraConfigs.Remove(config);
    }

    [ReactiveCommand]
    private async Task AddLicensePlateRecognitionAsync()
    {
        var dialogViewModel = new AddLprDialogViewModel(LprDeviceType)
        {
            Name = $"camera_{LicensePlateRecognitionConfigs.Count + 1}",
            Direction = LicensePlateDirection.In
        };

        var dialog = new AddLprDialog(dialogViewModel);
        var window = GetWindow();
        var result = await dialog.ShowDialog<LicensePlateRecognitionConfigViewModel?>(window);

        if (result != null)
        {
            LicensePlateRecognitionConfigs.Add(result);
        }
    }

    [ReactiveCommand]
    private async Task EditCameraAsync(CameraConfigViewModel? config)
    {
        if (config == null) return;

        var dialogViewModel = new AddCameraDialogViewModel
        {
            Name = config.Name,
            Ip = config.Ip,
            Port = config.Port,
            Channel = config.Channel,
            UserName = config.UserName,
            Password = config.Password
        };

        var dialog = new AddCameraDialog(dialogViewModel);
        var window = GetWindow();
        var result = await dialog.ShowDialog<CameraConfigViewModel?>(window);

        if (result != null)
        {
            var index = CameraConfigs.IndexOf(config);
            if (index >= 0)
            {
                CameraConfigs[index] = result;
            }
        }
    }

    [ReactiveCommand]
    private async Task EditLprAsync(LicensePlateRecognitionConfigViewModel? config)
    {
        if (config == null) return;

        var dialogViewModel = new AddLprDialogViewModel(LprDeviceType)
        {
            Name = config.Name,
            Ip = config.Ip,
            Direction = config.Direction,
            UserName = config.UserName,
            Password = config.Password,
            Port = config.Port,
            Channel = config.Channel
        };

        var dialog = new AddLprDialog(dialogViewModel);
        var window = GetWindow();
        var result = await dialog.ShowDialog<LicensePlateRecognitionConfigViewModel?>(window);

        if (result != null)
        {
            var index = LicensePlateRecognitionConfigs.IndexOf(config);
            if (index >= 0)
            {
                LicensePlateRecognitionConfigs[index] = result;
            }
        }
    }

    [ReactiveCommand]
    private void RemoveLicensePlateRecognition(LicensePlateRecognitionConfigViewModel? config)
    {
        if (config != null) LicensePlateRecognitionConfigs.Remove(config);
    }

    [ReactiveCommand]
    private async Task TestCaptureAsync()
    {
        try
        {
            _logger.LogInformation("开始测试拍照...");
            // Call test capture service method - it will handle all cameras and send notification
            var results = await _hikvisionService.TestCaptureAsync();
            var successCount = results.Count(r => r.Success);
            var failCount = results.Count - successCount;
            _logger.LogInformation("测试拍照完成，成功: {SuccessCount}, 失败: {FailCount}", successCount, failCount);

            // Log detailed results
            foreach (var result in results)
            {
                if (result.Success)
                {
                    _logger.LogInformation(
                        "拍照成功 - 设备: {DeviceKey}, 通道: {Channel}, 文件: {FilePath}, 大小: {FileSize} bytes",
                        result.Request.DeviceKey, result.Request.Channel, result.Request.SaveFullPath, result.FileSize);
                }
                else
                {
                    _logger.LogWarning("拍照失败 - 设备: {DeviceKey}, 通道: {Channel}, 错误: {ErrorMessage}",
                        result.Request.DeviceKey, result.Request.Channel, result.ErrorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试拍照时发生异常");
            // Show error message - in a real app you'd use a dialog service
            // Error: ex.Message
        }
    }

    #endregion

    #region Helper Methods

    private Window GetWindow()
    {
        // Helper to get current window instance for ShowDialog parent
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.Windows.FirstOrDefault(w => w.DataContext == this)
                   ?? throw new InvalidOperationException("Cannot find window");
        }

        throw new InvalidOperationException("Application is not running in desktop mode");
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Refresh available serial ports from system
    /// </summary>
    private void RefreshAvailableSerialPorts()
    {
        try
        {
            var ports = SerialPort.GetPortNames().OrderBy(p => p).ToList();
            AvailableSerialPorts.Clear();
            foreach (var port in ports) AvailableSerialPorts.Add(port);

            // If current selected port is not in the list, add it (might be disconnected)
            if (!string.IsNullOrEmpty(ScaleSerialPort) && !AvailableSerialPorts.Contains(ScaleSerialPort))
                AvailableSerialPorts.Insert(0, ScaleSerialPort);
        }
        catch
        {
            // If getting ports fails, keep existing list
        }
    }

    private void RefreshAvailablePrinters()
    {
        try
        {
            var printers = _ticketPrintingService.ListInstalledPrinters()
                .OrderBy(p => p)
                .ToList();

            AvailablePrinters.Clear();
            foreach (var printer in printers)
                AvailablePrinters.Add(printer);

            // If current selected printer is not in the list, add it (might be disconnected/renamed)
            if (!string.IsNullOrWhiteSpace(SelectedPrinterName) &&
                !AvailablePrinters.Contains(SelectedPrinterName))
            {
                AvailablePrinters.Insert(0, SelectedPrinterName);
            }
        }
        catch
        {
            // If getting printers fails, keep existing list
        }
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();

            // Load scale settings
            ScaleSerialPort = settings.ScaleSettings.SerialPort;
            ScaleBaudRate = settings.ScaleSettings.BaudRate;
            ScaleCommunicationMethod = settings.ScaleSettings.CommunicationMethod;
            ScaleUnit = settings.ScaleSettings.ScaleUnit;
            ScaleType = settings.ScaleSettings.ScaleType;

            // Ensure the loaded serial port is in the available list
            if (!string.IsNullOrEmpty(ScaleSerialPort) && !AvailableSerialPorts.Contains(ScaleSerialPort))
                AvailableSerialPorts.Insert(0, ScaleSerialPort);

            // Load document scanner settings
            DocumentScannerUsbDevice = settings.DocumentScannerConfig.UsbDevice;

            // Load system settings
            EnableAutoStart = settings.SystemSettings.EnableAutoStart;
            CaptureStreamType = settings.SystemSettings.CaptureStreamType;
            Urls = settings.SystemSettings.Urls;
            LprDeviceType = settings.SystemSettings.LprDeviceType;
            EnablePrinter = settings.SystemSettings.EnablePrinter;
            SelectedPrinterName = settings.SystemSettings.SelectedPrinterName;

            // Ensure the loaded printer is in the available list (might be disconnected)
            if (!string.IsNullOrWhiteSpace(SelectedPrinterName) &&
                !AvailablePrinters.Contains(SelectedPrinterName))
            {
                AvailablePrinters.Insert(0, SelectedPrinterName);
            }

            // Load weighing configuration
            MinWeightThreshold = settings.WeighingConfiguration.MinWeightThreshold;
            WeightStabilityThreshold = settings.WeighingConfiguration.WeightStabilityThreshold;
            StabilityWindowMs = settings.WeighingConfiguration.StabilityWindowMs;
            StabilityCheckIntervalMs = settings.WeighingConfiguration.StabilityCheckIntervalMs;
            MaxIntervalMinutes = settings.WeighingConfiguration.MaxIntervalMinutes;
            MinWeightDiff = settings.WeighingConfiguration.MinWeightDiff;

            // Load camera configs
            CameraConfigs.Clear();
            foreach (var config in settings.CameraConfigs)
                CameraConfigs.Add(new CameraConfigViewModel
                {
                    Name = config.Name,
                    Ip = config.Ip,
                    Port = config.Port,
                    Channel = config.Channel,
                    UserName = config.UserName,
                    Password = config.Password
                });

            // Load license plate recognition configs
            LicensePlateRecognitionConfigs.Clear();
            foreach (var config in settings.LicensePlateRecognitionConfigs)
                LicensePlateRecognitionConfigs.Add(new LicensePlateRecognitionConfigViewModel
                {
                    Name = config.Name,
                    Ip = config.Ip,
                    Direction = config.Direction,
                    UserName = config.UserName,
                    Password = config.Password,
                    Port = config.Port,
                    Channel = config.Channel ?? HikvisionLprDefaults.DefaultChannel
                });

            // Load sound device settings
            SoundDeviceEnabled = settings.SoundDeviceSettings.Enabled;
            SoundDeviceLocalIP = settings.SoundDeviceSettings.LocalIP;
            SoundDeviceSoundIP = settings.SoundDeviceSettings.SoundIP;
            SoundDeviceSoundSN = settings.SoundDeviceSettings.SoundSN;
            SoundDeviceSoundVolume = settings.SoundDeviceSettings.SoundVolume;
        }
        catch
        {
            // If loading fails, use default values
        }
    }

    #endregion
}

/// <summary>
///     Camera config ViewModel for UI binding
/// </summary>
public partial class CameraConfigViewModel : ReactiveObject
{
    [Reactive] private string _channel = string.Empty;

    [Reactive] private string _ip = string.Empty;

    [Reactive] private string _name = string.Empty;

    [Reactive] private string _password = string.Empty;

    [Reactive] private string _port = string.Empty;

    [Reactive] private string _userName = string.Empty;
}

/// <summary>
///     License plate recognition config ViewModel for UI binding
/// </summary>
public partial class LicensePlateRecognitionConfigViewModel : ReactiveObject
{
    [Reactive] private LicensePlateDirection _direction = LicensePlateDirection.In;

    [Reactive] private string _ip = string.Empty;

    [Reactive] private string _name = string.Empty;

    [Reactive] private string? _userName;

    [Reactive] private string? _password;

    [Reactive] private string? _port;

    [Reactive] private string? _channel;

    /// <summary>
    ///     设备是否在线（由 10 分钟定时检查更新）
    /// </summary>
    [Reactive] private bool _isOnline;

    /// <summary>
    ///     在线状态显示文本
    /// </summary>
    public string OnlineStatusText => IsOnline ? "在线" : "离线";

    public LicensePlateRecognitionConfigViewModel()
    {
        this.WhenAnyValue(x => x.Direction)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(DirectionIndex));
                this.RaisePropertyChanged(nameof(DirectionText));
            });
        this.WhenAnyValue(x => x.IsOnline)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(OnlineStatusText)));
    }

    /// <summary>
    ///     Direction as int for ComboBox binding
    /// </summary>
    public int DirectionIndex
    {
        get => (int)Direction;
        set
        {
            if (value is >= 0 and <= 1) Direction = (LicensePlateDirection)value;
        }
    }

    /// <summary>
    ///     Direction as text for display
    /// </summary>
    public string DirectionText => Direction == LicensePlateDirection.In ? "进场" : "出场";
}