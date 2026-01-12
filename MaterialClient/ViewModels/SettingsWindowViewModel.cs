using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
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
    [Reactive] private SnapshotCameraType _snapshotCameraType = SnapshotCameraType.Hikvision;

    // License plate recognition configs
    [Reactive] private ObservableCollection<LicensePlateRecognitionConfigViewModel> _licensePlateRecognitionConfigs =
        new();

    [Reactive] private string _scaleBaudRate = "9600";

    [Reactive] private string _scaleCommunicationMethod = "TF0";

    // Scale settings
    [Reactive] private string _scaleSerialPort = "COM3";
    [Reactive] private ScaleUnit _scaleUnit = ScaleUnit.Ton;
    [Reactive] private ScaleType _scaleType = ScaleType.Default;

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
        ScaleType.Default,
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
    ///     Snapshot camera type options for ComboBox
    /// </summary>
    public ObservableCollection<SnapshotCameraType> SnapshotCameraTypeOptions { get; } = new()
    {
        SnapshotCameraType.Hikvision,
        SnapshotCameraType.LprAllInOne
    };

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

    public SettingsWindowViewModel(
        ISettingsService settingsService,
        ITruckScaleWeightService truckScaleWeightService,
        IHikvisionService hikvisionService,
        ILogger<SettingsWindowViewModel> logger)
    {
        _settingsService = settingsService;
        _truckScaleWeightService = truckScaleWeightService;
        _hikvisionService = hikvisionService;
        _logger = logger;

        // Load available serial ports
        RefreshAvailableSerialPorts();

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
                new SystemSettings
                {
                    EnableAutoStart = EnableAutoStart,
                    CaptureStreamType = CaptureStreamType,
                    Urls = Urls,
                    SnapshotCameraType = SnapshotCameraType
                },
                CameraConfigs.Select(c => new CameraConfig
                {
                    Name = c.Name,
                    Ip = c.Ip,
                    Port = c.Port,
                    Channel = c.Channel,
                    UserName = c.UserName,
                    Password = c.Password
                }).ToList(),
                LicensePlateRecognitionConfigs.Select(l => new LicensePlateRecognitionConfig
                {
                    Name = l.Name,
                    Ip = l.Ip,
                    Direction = l.Direction
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
    private void AddCamera()
    {
        CameraConfigs.Add(new CameraConfigViewModel
        {
            Name = $"camera_{CameraConfigs.Count + 1}"
        });
    }

    [ReactiveCommand]
    private void RemoveCamera(CameraConfigViewModel? config)
    {
        if (config != null) CameraConfigs.Remove(config);
    }

    [ReactiveCommand]
    private void AddLicensePlateRecognition()
    {
        LicensePlateRecognitionConfigs.Add(new LicensePlateRecognitionConfigViewModel
        {
            Name = $"camera_{LicensePlateRecognitionConfigs.Count + 1}",
            Direction = LicensePlateDirection.In
        });
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
                    _logger.LogInformation("拍照成功 - 设备: {DeviceKey}, 通道: {Channel}, 文件: {FilePath}, 大小: {FileSize} bytes",
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
            SnapshotCameraType = settings.SystemSettings.SnapshotCameraType;

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
                    Direction = config.Direction
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

    public LicensePlateRecognitionConfigViewModel()
    {
        this.WhenAnyValue(x => x.Direction)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(DirectionIndex)));
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
}