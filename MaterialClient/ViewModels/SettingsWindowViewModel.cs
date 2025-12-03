using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using ReactiveUI;

namespace MaterialClient.ViewModels;

/// <summary>
/// Settings window ViewModel
/// </summary>
public class SettingsWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    
    // Scale settings
    private string _scaleSerialPort = "COM3";
    private string _scaleBaudRate = "9600";
    private string _scaleCommunicationMethod = "TF0";
    
    // Document scanner settings
    private string? _documentScannerUsbDevice;
    
    // System settings
    private bool _enableAutoStart = false;
    
    // Camera configs
    private ObservableCollection<CameraConfigViewModel> _cameraConfigs = new();
    
    // License plate recognition configs
    private ObservableCollection<LicensePlateRecognitionConfigViewModel> _licensePlateRecognitionConfigs = new();

    public SettingsWindowViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
        CancelCommand = ReactiveCommand.Create(OnCancel);
        AddCameraCommand = ReactiveCommand.Create(AddCamera);
        RemoveCameraCommand = ReactiveCommand.Create<CameraConfigViewModel>(RemoveCamera);
        AddLicensePlateRecognitionCommand = ReactiveCommand.Create(AddLicensePlateRecognition);
        RemoveLicensePlateRecognitionCommand = ReactiveCommand.Create<LicensePlateRecognitionConfigViewModel>(RemoveLicensePlateRecognition);
        
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

    #region Methods

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            
            // Load scale settings
            ScaleSerialPort = settings.ScaleSettings.SerialPort;
            ScaleBaudRate = settings.ScaleSettings.BaudRate;
            ScaleCommunicationMethod = settings.ScaleSettings.CommunicationMethod;
            
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
            
            // Close window after saving
            OnCancel();
        }
        catch
        {
            // Handle error
        }
    }

    private void OnCancel()
    {
        // Window will be closed by the caller
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
