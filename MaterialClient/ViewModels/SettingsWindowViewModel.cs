using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using ReactiveUI;

namespace MaterialClient.ViewModels;

/// <summary>
/// Settings window ViewModel
/// </summary>
public class SettingsWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    
    // Scale settings
    private string _scaleSerialPort = "COM3";
    private string _scaleBaudRate = "9600";
    private string _scaleCommunicationMethod = "TF0";
    private ObservableCollection<string> _availableSerialPorts = new();
    
    // Document scanner settings
    private string? _documentScannerUsbDevice;
    
    // System settings
    private bool _enableAutoStart = false;
    
    // Camera configs
    private ObservableCollection<CameraConfigViewModel> _cameraConfigs = new();
    
    // License plate recognition configs
    private ObservableCollection<LicensePlateRecognitionConfigViewModel> _licensePlateRecognitionConfigs = new();

    public SettingsWindowViewModel(
        ISettingsService settingsService,
        ITruckScaleWeightService truckScaleWeightService)
    {
        _settingsService = settingsService;
        _truckScaleWeightService = truckScaleWeightService;
        
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
        CancelCommand = ReactiveCommand.Create(OnCancel);
        AddCameraCommand = ReactiveCommand.Create(AddCamera);
        RemoveCameraCommand = ReactiveCommand.Create<CameraConfigViewModel>(RemoveCamera);
        AddLicensePlateRecognitionCommand = ReactiveCommand.Create(AddLicensePlateRecognition);
        RemoveLicensePlateRecognitionCommand = ReactiveCommand.Create<LicensePlateRecognitionConfigViewModel>(RemoveLicensePlateRecognition);
        
        // Load available serial ports
        RefreshAvailableSerialPorts();
        
        // Load settings
        _ = LoadSettingsAsync();
    }

    #region Scale Settings Properties

    public string ScaleSerialPort
    {
        get => _scaleSerialPort;
        set => this.RaiseAndSetIfChanged(ref _scaleSerialPort, value);
    }

    public string ScaleBaudRate
    {
        get => _scaleBaudRate;
        set => this.RaiseAndSetIfChanged(ref _scaleBaudRate, value);
    }

    public string ScaleCommunicationMethod
    {
        get => _scaleCommunicationMethod;
        set => this.RaiseAndSetIfChanged(ref _scaleCommunicationMethod, value);
    }

    public ObservableCollection<string> AvailableSerialPorts
    {
        get => _availableSerialPorts;
        set => this.RaiseAndSetIfChanged(ref _availableSerialPorts, value);
    }

    #endregion

    #region Document Scanner Properties

    public string? DocumentScannerUsbDevice
    {
        get => _documentScannerUsbDevice;
        set => this.RaiseAndSetIfChanged(ref _documentScannerUsbDevice, value);
    }

    #endregion

    #region System Settings Properties

    public bool EnableAutoStart
    {
        get => _enableAutoStart;
        set => this.RaiseAndSetIfChanged(ref _enableAutoStart, value);
    }

    #endregion

    #region Camera Configs Properties

    public ObservableCollection<CameraConfigViewModel> CameraConfigs
    {
        get => _cameraConfigs;
        set => this.RaiseAndSetIfChanged(ref _cameraConfigs, value);
    }

    #endregion

    #region License Plate Recognition Configs Properties

    public ObservableCollection<LicensePlateRecognitionConfigViewModel> LicensePlateRecognitionConfigs
    {
        get => _licensePlateRecognitionConfigs;
        set => this.RaiseAndSetIfChanged(ref _licensePlateRecognitionConfigs, value);
    }

    #endregion

    #region Commands

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AddCameraCommand { get; }
    public ICommand RemoveCameraCommand { get; }
    public ICommand AddLicensePlateRecognitionCommand { get; }
    public ICommand RemoveLicensePlateRecognitionCommand { get; }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the window should be closed
    /// </summary>
    public event EventHandler? CloseRequested;

    #endregion

    #region Methods

    /// <summary>
    /// Refresh available serial ports from system
    /// </summary>
    private void RefreshAvailableSerialPorts()
    {
        try
        {
            var ports = SerialPort.GetPortNames().OrderBy(p => p).ToList();
            AvailableSerialPorts.Clear();
            foreach (var port in ports)
            {
                AvailableSerialPorts.Add(port);
            }
            
            // If current selected port is not in the list, add it (might be disconnected)
            if (!string.IsNullOrEmpty(ScaleSerialPort) && !AvailableSerialPorts.Contains(ScaleSerialPort))
            {
                AvailableSerialPorts.Insert(0, ScaleSerialPort);
            }
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
            {
                AvailableSerialPorts.Insert(0, ScaleSerialPort);
            }
            
            // Load document scanner settings
            DocumentScannerUsbDevice = settings.DocumentScannerConfig.UsbDevice;
            
            // Load system settings
            EnableAutoStart = settings.SystemSettings.EnableAutoStart;
            
            // Load camera configs
            CameraConfigs.Clear();
            foreach (var config in settings.CameraConfigs)
            {
                CameraConfigs.Add(new CameraConfigViewModel
                {
                    Name = config.Name,
                    Ip = config.Ip,
                    Port = config.Port,
                    Channel = config.Channel,
                    UserName = config.UserName,
                    Password = config.Password
                });
            }
            
            // Load license plate recognition configs
            LicensePlateRecognitionConfigs.Clear();
            foreach (var config in settings.LicensePlateRecognitionConfigs)
            {
                LicensePlateRecognitionConfigs.Add(new LicensePlateRecognitionConfigViewModel
                {
                    Name = config.Name,
                    Ip = config.Ip,
                    Direction = config.Direction
                });
            }
        }
        catch
        {
            // If loading fails, use default values
        }
    }

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
                }).ToList()
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

    private void OnCancel()
    {
        // Raise close requested event
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AddCamera()
    {
        CameraConfigs.Add(new CameraConfigViewModel
        {
            Name = $"camera_{CameraConfigs.Count + 1}"
        });
    }

    private void RemoveCamera(CameraConfigViewModel? config)
    {
        if (config != null)
        {
            CameraConfigs.Remove(config);
        }
    }

    private void AddLicensePlateRecognition()
    {
        LicensePlateRecognitionConfigs.Add(new LicensePlateRecognitionConfigViewModel
        {
            Name = $"camera_{LicensePlateRecognitionConfigs.Count + 1}",
            Direction = LicensePlateDirection.In
        });
    }

    private void RemoveLicensePlateRecognition(LicensePlateRecognitionConfigViewModel? config)
    {
        if (config != null)
        {
            LicensePlateRecognitionConfigs.Remove(config);
        }
    }

    #endregion
}

/// <summary>
/// Camera config ViewModel for UI binding
/// </summary>
public class CameraConfigViewModel : ReactiveObject
{
    private string _name = string.Empty;
    private string _ip = string.Empty;
    private string _port = string.Empty;
    private string _channel = string.Empty;
    private string _userName = string.Empty;
    private string _password = string.Empty;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public string Ip
    {
        get => _ip;
        set => this.RaiseAndSetIfChanged(ref _ip, value);
    }

    public string Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    public string Channel
    {
        get => _channel;
        set => this.RaiseAndSetIfChanged(ref _channel, value);
    }

    public string UserName
    {
        get => _userName;
        set => this.RaiseAndSetIfChanged(ref _userName, value);
    }

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }
}

/// <summary>
/// License plate recognition config ViewModel for UI binding
/// </summary>
public class LicensePlateRecognitionConfigViewModel : ReactiveObject
{
    private string _name = string.Empty;
    private string _ip = string.Empty;
    private LicensePlateDirection _direction = LicensePlateDirection.In;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public string Ip
    {
        get => _ip;
        set => this.RaiseAndSetIfChanged(ref _ip, value);
    }

    public LicensePlateDirection Direction
    {
        get => _direction;
        set => this.RaiseAndSetIfChanged(ref _direction, value);
    }

    /// <summary>
    /// Direction as int for ComboBox binding
    /// </summary>
    public int DirectionIndex
    {
        get => (int)_direction;
        set
        {
            if (value >= 0 && value <= 1)
            {
                Direction = (LicensePlateDirection)value;
            }
        }
    }
}
