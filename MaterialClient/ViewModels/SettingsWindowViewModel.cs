using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.ViewModels;

/// <summary>
///     Settings window ViewModel
/// </summary>
public partial class SettingsWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly ITruckScaleWeightService _truckScaleWeightService;

    [Reactive] private ObservableCollection<string> _availableSerialPorts = new();

    // Camera configs
    [Reactive] private ObservableCollection<CameraConfigViewModel> _cameraConfigs = new();

    // Document scanner settings
    [Reactive] private string? _documentScannerUsbDevice;

    // System settings
    [Reactive] private bool _enableAutoStart;

    // License plate recognition configs
    [Reactive] private ObservableCollection<LicensePlateRecognitionConfigViewModel> _licensePlateRecognitionConfigs =
        new();

    [Reactive] private string _scaleBaudRate = "9600";

    [Reactive] private string _scaleCommunicationMethod = "TF0";

    // Scale settings
    [Reactive] private string _scaleSerialPort = "COM3";

    // Weighing configuration
    [Reactive] private decimal _minWeightThreshold = 0.5m;
    [Reactive] private decimal _weightStabilityThreshold = 0.05m;
    [Reactive] private int _stabilityWindowMs = 3000;
    [Reactive] private int _stabilityCheckIntervalMs = 200;
    [Reactive] private int _maxIntervalMinutes = 300;
    [Reactive] private decimal _minWeightDiff = 1m;

    public SettingsWindowViewModel(
        ISettingsService settingsService,
        ITruckScaleWeightService truckScaleWeightService)
    {
        _settingsService = settingsService;
        _truckScaleWeightService = truckScaleWeightService;

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
                    CommunicationMethod = ScaleCommunicationMethod
                },
                new DocumentScannerConfig
                {
                    UsbDevice = DocumentScannerUsbDevice
                },
                new SystemSettings
                {
                    EnableAutoStart = EnableAutoStart
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

            // Ensure the loaded serial port is in the available list
            if (!string.IsNullOrEmpty(ScaleSerialPort) && !AvailableSerialPorts.Contains(ScaleSerialPort))
                AvailableSerialPorts.Insert(0, ScaleSerialPort);

            // Load document scanner settings
            DocumentScannerUsbDevice = settings.DocumentScannerConfig.UsbDevice;

            // Load system settings
            EnableAutoStart = settings.SystemSettings.EnableAutoStart;

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