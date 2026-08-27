using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.UI.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.UI.ViewModels;

/// <summary>
///     Settings window ViewModel
/// </summary>
public partial class SettingsWindowViewModel : ViewModelBase, ITransientDependency, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly IHikvisionService _hikvisionService;
    private readonly ITicketPrintingService _ticketPrintingService;
    private readonly ILogger<SettingsWindowViewModel> _logger;
    private readonly ISoundDeviceService _soundDeviceService;
    private readonly ILprDeviceResolver _lprDeviceResolver;
    private readonly ILocalEventBus _localEventBus;
    private readonly IUsbCameraService? _usbCameraService;
    private readonly IXiaoshanUploadConfigClientFacade? _xiaoshanUploadConfigFacade;
    private readonly IDisposable _lprMessageSubscription;

    private bool _urbanConfigDirty;
    private bool _urbanConfigLoading;

    [Reactive] private ObservableCollection<string> _availableSerialPorts = new();

    // Camera configs
    [Reactive] private ObservableCollection<CameraConfigViewModel> _cameraConfigs = new();

    // Document camera (高拍仪) settings
    [Reactive] private bool _documentCameraEnabled;
    [Reactive] private string? _documentScannerUsbDevice;
    [Reactive] private string? _documentCameraTestResult;

    // System settings
    [Reactive] private bool _enableAutoStart;
    [Reactive] private bool _enableChunkedAttachmentUpload;
    [Reactive] private StreamType _captureStreamType = StreamType.Substream;
    [Reactive] private string _urls = "http://localhost:9960";
    [Reactive] private LprDeviceType _lprDeviceType = LprDeviceType.Hikvision;
    [Reactive] private bool _enablePrinter;
    [Reactive] private string _selectedPrinterName = string.Empty;
    [Reactive] private bool _enableLatestRecommendation;
    [Reactive] private bool _enableTriggerLprCapture;
    [Reactive] private int _triggerLprCaptureDelayMs;
    [Reactive] private int _jpegQuality = 75;
    [Reactive] private bool _showUrbanAnomalyDetectionSettings;
    [Reactive] private bool _showUrbanConfigSettings;
    [Reactive] private string _selectedSettingsSection = "ScaleSettings";
    [Reactive] private DeliveryType _defaultDeliveryType = DeliveryType.Receiving;
    [Reactive] private decimal _urbanAnomalyUpperLimit = 30.0m;
    [Reactive] private decimal _urbanAnomalyLowerLimit = 2.0m;
    [Reactive] private decimal _urbanAnomalyDeviationPercentage = 10.0m;

    // Urban / Xiaoshan upload config (modes only in UI; remark/static fields preserved for push)
    [Reactive] private string? _urbanConfigDisplayName;
    [Reactive] private bool _urbanConfigWeighbridgeEnabled = true;
    [Reactive] private bool _urbanConfigGateEnabled;
    [Reactive] private bool _urbanConfigProductEnabled;
    [Reactive] private string? _urbanConfigWbInOutType;
    [Reactive] private string? _urbanConfigGateDeviceId;
    [Reactive] private string? _urbanConfigGateSiteType;
    [Reactive] private string? _urbanConfigProductDeviceId;
    [Reactive] private string? _urbanConfigProductSiteType;
    [Reactive] private string? _urbanConfigStatusMessage;
    [Reactive] private bool _isUrbanConfigBusy;

    /// <summary>Preserved for server push; not edited in settings UI.</summary>
    private string? _preservedUrbanRemark;
    private string? _preservedBuildLicenseNo;
    private string? _preservedAreaCode;
    private string? _preservedSpaceName;

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
        ScaleType.DingSong,
        ScaleType.TestMode,
        ScaleType.PortableXPSY,
        ScaleType.DingSongAddr4
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
        LprDeviceType.Vzvision,
        LprDeviceType.Huaxiazhixin
    };

    /// <summary>
    ///     收发料类型选项（单一数据源）
    /// </summary>
    public ObservableCollection<DeliveryType> DeliveryTypeOptions { get; } = new()
    {
        DeliveryType.Receiving,
        DeliveryType.Sending
    };

    /// <summary>
    ///     是否显示海康威视专用配置字段
    /// </summary>
    public bool ShowHikvisionLprFields => LprDeviceType == LprDeviceType.Hikvision;

    /// <summary>
    ///     列表中是否显示用户名、端口列（海康与臻识 Vzvision）
    /// </summary>
    public bool ShowLprUserPortColumns =>
        LprDeviceType is LprDeviceType.Hikvision or LprDeviceType.Vzvision;

    // Weighing configuration
    [Reactive] private decimal _minWeightThreshold = 0.5m;
    [Reactive] private decimal _weightStabilityThreshold = 0.05m;
    [Reactive] private int _stabilityWindowMs = 3000;
    [Reactive] private int _stabilityCheckIntervalMs = 200;
    [Reactive] private int _maxIntervalMinutes = 300;
    [Reactive] private decimal _minWeightDiff = 1m;
    [Reactive] private bool _enablePlateRewrite = true;
    [Reactive] private bool _enableLatestPlateNumber = false;
    [Reactive] private bool _enableMatchOnStable = false;
    [Reactive] private string _gateIoValidationErrorMessage = string.Empty;
    [Reactive] private bool _hasGateIoValidationError;

    // Sound device settings
    [Reactive] private bool _soundDeviceEnabled = false;
    [Reactive] private string _soundDeviceLocalIP = string.Empty;
    [Reactive] private string _soundDeviceSoundIP = string.Empty;
    [Reactive] private string _soundDeviceSoundSN = string.Empty;
    [Reactive] private string _soundDeviceSoundVolume = "0";

    // Sound device test status
    [Reactive] private bool _isSoundDeviceTestRunning = false;
    [Reactive] private string? _soundDeviceTestResult = null;

    public ObservableCollection<string> AvailablePrinters { get; } = new();

    public SettingsWindowViewModel(
        ISettingsService settingsService,
        ITruckScaleWeightService truckScaleWeightService,
        IHikvisionService hikvisionService,
        ITicketPrintingService ticketPrintingService,
        ILogger<SettingsWindowViewModel> logger,
        ISoundDeviceService soundDeviceService,
        ILprDeviceResolver lprDeviceResolver,
        ILocalEventBus localEventBus,
        IServiceProvider serviceProvider,
        IUsbCameraService? usbCameraService = null)
    {
        _settingsService = settingsService;
        _truckScaleWeightService = truckScaleWeightService;
        _hikvisionService = hikvisionService;
        _ticketPrintingService = ticketPrintingService;
        _logger = logger;
        _soundDeviceService = soundDeviceService;
        _lprDeviceResolver = lprDeviceResolver;
        _localEventBus = localEventBus;
        // Resolve optional Urban-only services from the host container (optional ctor params
        // with defaults are unreliable under Autofac and may stay null on MaterialClient.Urban).
        _usbCameraService = usbCameraService ?? serviceProvider.GetService<IUsbCameraService>();
        _xiaoshanUploadConfigFacade = serviceProvider.GetService<IXiaoshanUploadConfigClientFacade>();

        // Subscribe to LPR recognition messages and update the matching row's LastCapturePlateNumber
        _lprMessageSubscription = MessageBus.Current.Listen<LicensePlateRecognizedMessage>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(msg =>
            {
                var item = LicensePlateRecognitionConfigs.FirstOrDefault(c =>
                    string.Equals(c.Name, msg.DeviceName, StringComparison.Ordinal));
                if (item != null)
                    item.LastCapturePlateNumber = msg.PlateNumber ?? string.Empty;
            });

        // Subscribe to LprDeviceType changes to notify LPR-related visibility properties
        this.WhenAnyValue(x => x.LprDeviceType)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(ShowHikvisionLprFields));
                this.RaisePropertyChanged(nameof(ShowLprUserPortColumns));
            });

        this.WhenAnyValue(
                x => x.UrbanConfigDisplayName,
                x => x.UrbanConfigWeighbridgeEnabled,
                x => x.UrbanConfigGateEnabled,
                x => x.UrbanConfigProductEnabled)
            .Subscribe(_ => MarkUrbanConfigDirty());
        this.WhenAnyValue(
                x => x.UrbanConfigWbInOutType,
                x => x.UrbanConfigGateDeviceId,
                x => x.UrbanConfigGateSiteType,
                x => x.UrbanConfigProductDeviceId,
                x => x.UrbanConfigProductSiteType)
            .Subscribe(_ => MarkUrbanConfigDirty());

        // Load available serial ports
        RefreshAvailableSerialPorts();
        RefreshAvailablePrinters();

        // Load settings
        _ = LoadSettingsAsync();
    }

    private void MarkUrbanConfigDirty()
    {
        if (_urbanConfigLoading || !ShowUrbanConfigSettings)
        {
            return;
        }

        _urbanConfigDirty = true;
    }

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
            systemSettings.EnableChunkedAttachmentUpload = EnableChunkedAttachmentUpload;
            systemSettings.CaptureStreamType = CaptureStreamType;
            systemSettings.Urls = Urls;
            systemSettings.LprDeviceType = LprDeviceType;
            systemSettings.DocumentCameraEnabled = DocumentCameraEnabled;
            systemSettings.EnablePrinter = EnablePrinter;
            systemSettings.SelectedPrinterName = SelectedPrinterName;
            systemSettings.EnableLatestRecommendation = EnableLatestRecommendation;
            systemSettings.EnableTriggerLprCapture = EnableTriggerLprCapture;
            systemSettings.TriggerLprCaptureDelayMs = Math.Max(0, TriggerLprCaptureDelayMs);
            systemSettings.JpegQuality = JpegQuality;
            systemSettings.DefaultDeliveryType = DefaultDeliveryType;
            systemSettings.UrbanAnomalyDetection = new UrbanAnomalyDetectionConfig
            {
                UpperLimit = UrbanAnomalyUpperLimit,
                LowerLimit = UrbanAnomalyLowerLimit,
                DeviationPercentage = UrbanAnomalyDeviationPercentage
            };

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
                        Channel = l.Channel,
                        EnableGateIo = l.EnableGateIo,
                        IoChannel = l.IoChannel
                    };
                    if (HikvisionLprDefaults.ShouldApply(LprDeviceType))
                        HikvisionLprDefaults.ApplyDefaults(config);
                    else if (VzvisionLprDefaults.ShouldApply(LprDeviceType))
                        VzvisionLprDefaults.ApplyDefaults(config);
                    return config;
                }).ToList(),
                new WeighingConfiguration
                {
                    MinWeightThreshold = MinWeightThreshold,
                    WeightStabilityThreshold = WeightStabilityThreshold,
                    StabilityWindowMs = StabilityWindowMs,
                    StabilityCheckIntervalMs = StabilityCheckIntervalMs,
                    MaxIntervalMinutes = MaxIntervalMinutes,
                    MinWeightDiff = MinWeightDiff,
                    EnablePlateRewrite = EnablePlateRewrite,
                    EnableLatestPlateNumber = EnableLatestPlateNumber,
                    EnableMatchOnStable = EnableMatchOnStable
                },
                new SoundDeviceSettings
                {
                    Enabled = SoundDeviceEnabled,
                    LocalIP = SoundDeviceLocalIP,
                    SoundIP = SoundDeviceSoundIP,
                    SoundSN = SoundDeviceSoundSN,
                    SoundVolume = SoundDeviceSoundVolume
                },
                BuildUrbanSettingsForSave(existingSettings.UrbanSettings)
            );

            await _settingsService.SaveSettingsAsync(settings);

            // Restart truck scale service with new settings
            await _truckScaleWeightService.RestartAsync();

            // Common + UI both consume via EventData → bridge → SettingsSavedMessage
            await _localEventBus.PublishAsync(new SettingsSavedEventData());

            if (ShowUrbanConfigSettings && _urbanConfigDirty && _xiaoshanUploadConfigFacade is not null)
            {
                var pushed = await PushUrbanConfigViaLocalEventAsync();
                if (!pushed)
                {
                    return;
                }
            }

            MessageBus.Current.SendMessage(new DetailCloseRequestedMessage());
        }
        catch
        {
            // Handle error
        }
    }

    private async Task<bool> PushUrbanConfigViaLocalEventAsync()
    {
        IsUrbanConfigBusy = true;
        try
        {
            var payload = BuildUrbanConfigJson();
            var evt = new XiaoshanUploadConfigSaveRequestedEventData
            {
                DisplayName = UrbanConfigDisplayName,
                Remark = _preservedUrbanRemark,
                ModesJson = payload.ModesJson,
                SettingsJson = payload.SettingsJson
            };

            await _localEventBus.PublishAsync(evt);
            var result = await evt.Completion.Task.WaitAsync(TimeSpan.FromSeconds(60));

            if (result.Success)
            {
                ApplyUrbanConfigSnapshot(
                    result.DisplayName,
                    result.Remark,
                    result.ModesJson,
                    result.SettingsJson);
                await PersistUrbanSettingsMirrorAsync();
                UrbanConfigStatusMessage = "已推送到服务器";
                _urbanConfigDirty = false;
                return true;
            }

            // Only discard local draft when server has an authoritative row to restore.
            if (result.HasServerRow)
            {
                ApplyUrbanConfigSnapshot(
                    result.DisplayName,
                    result.Remark,
                    result.ModesJson,
                    result.SettingsJson);
                await PersistUrbanSettingsMirrorAsync();
                UrbanConfigStatusMessage =
                    $"推送失败，已舍弃本地修改并恢复服务器配置。{result.Message}";
                _urbanConfigDirty = false;
            }
            else
            {
                UrbanConfigStatusMessage =
                    $"推送失败，服务器尚无权威配置，已保留本地编辑。{result.Message}";
                _urbanConfigDirty = true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Urban config LocalEvent push failed");
            UrbanConfigStatusMessage = $"推送失败: {ex.Message}";
            return false;
        }
        finally
        {
            IsUrbanConfigBusy = false;
        }
    }

    private UrbanSettings BuildUrbanSettingsForSave(UrbanSettings? existing)
    {
        if (!ShowUrbanConfigSettings)
        {
            return existing ?? new UrbanSettings();
        }

        return BuildUrbanSettingsFromForm(existing);
    }

    private void ApplyUrbanConfigFromLocalSettings(UrbanSettings urbanSettings)
    {
        var local = urbanSettings.XiaoshanUpload ?? new XiaoshanUploadLocalConfig();
        ApplyUrbanConfigSnapshot(
            local.DisplayName,
            local.Remark,
            local.ModesJson,
            local.SettingsJson);
        UrbanConfigStatusMessage = "已加载本地 UrbanSettings";
        _urbanConfigDirty = false;
    }

    private UrbanSettings BuildUrbanSettingsFromForm(UrbanSettings? existing)
    {
        var urban = existing ?? new UrbanSettings();
        var payload = BuildUrbanConfigJson();
        urban.XiaoshanUpload = new XiaoshanUploadLocalConfig
        {
            DisplayName = UrbanConfigDisplayName,
            Remark = _preservedUrbanRemark,
            ModesJson = payload.ModesJson,
            SettingsJson = payload.SettingsJson
        };
        return urban;
    }

    private async Task PersistUrbanSettingsMirrorAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            settings.UrbanSettings = BuildUrbanSettingsFromForm(settings.UrbanSettings);
            await _settingsService.SaveSettingsAsync(settings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist UrbanSettingsJson mirror");
        }
    }

    private void ApplyUrbanConfigSnapshot(
        string? displayName,
        string? remark,
        string modesJson,
        string settingsJson)
    {
        _urbanConfigLoading = true;
        try
        {
            UrbanConfigDisplayName = displayName;
            _preservedUrbanRemark = remark;

            var modes = XiaoshanUploadEnvelopeJson.ParseModes(modesJson);
            var settings = XiaoshanUploadEnvelopeJson.ParseSettings(settingsJson);

            UrbanConfigWeighbridgeEnabled = modes.IsEnabled(XiaoshanUploadModeNames.Weighbridge);
            UrbanConfigGateEnabled = modes.IsEnabled(XiaoshanUploadModeNames.Gate);
            UrbanConfigProductEnabled = modes.IsEnabled(XiaoshanUploadModeNames.Product);

            var wb = modes.GetSettings(XiaoshanUploadModeNames.Weighbridge);
            UrbanConfigWbInOutType = wb.InOutType;

            var gate = modes.GetSettings(XiaoshanUploadModeNames.Gate);
            UrbanConfigGateDeviceId = gate.DeviceId;
            UrbanConfigGateSiteType = gate.SiteType;

            var product = modes.GetSettings(XiaoshanUploadModeNames.Product);
            UrbanConfigProductDeviceId = product.DeviceId;
            UrbanConfigProductSiteType = product.SiteType;

            // Static fields are not edited in UI; keep last known values for push payloads.
            _preservedBuildLicenseNo = settings.BuildLicenseNo;
            _preservedAreaCode = settings.AreaCode;
            _preservedSpaceName = settings.SpaceName;
        }
        finally
        {
            _urbanConfigLoading = false;
        }
    }

    private sealed record UrbanConfigJsonPayload(string ModesJson, string SettingsJson);

    private UrbanConfigJsonPayload BuildUrbanConfigJson()
    {
        var enabled = new List<string>();
        if (UrbanConfigWeighbridgeEnabled) enabled.Add(XiaoshanUploadModeNames.Weighbridge);
        if (UrbanConfigGateEnabled) enabled.Add(XiaoshanUploadModeNames.Gate);
        if (UrbanConfigProductEnabled) enabled.Add(XiaoshanUploadModeNames.Product);

        var modes = new XiaoshanUploadModesEnvelope
        {
            EnabledModes = enabled,
            ModeSettings = new Dictionary<string, XiaoshanUploadModeSettings>(StringComparer.OrdinalIgnoreCase)
            {
                [XiaoshanUploadModeNames.Weighbridge] = new()
                {
                    InOutType = UrbanConfigWbInOutType,
                    DataSource = XiaoshanUploadDefaults.WeighbridgeDataSource
                },
                [XiaoshanUploadModeNames.Gate] = new()
                {
                    DeviceId = UrbanConfigGateDeviceId,
                    SiteType = UrbanConfigGateSiteType
                },
                [XiaoshanUploadModeNames.Product] = new()
                {
                    DeviceId = UrbanConfigProductDeviceId,
                    SiteType = UrbanConfigProductSiteType
                }
            }
        };

        var settings = new XiaoshanUploadSettingsEnvelope
        {
            BuildLicenseNo = _preservedBuildLicenseNo,
            AreaCode = _preservedAreaCode,
            SpaceName = _preservedSpaceName
        };

        return new UrbanConfigJsonPayload(
            XiaoshanUploadEnvelopeJson.SerializeModes(modes),
            XiaoshanUploadEnvelopeJson.SerializeSettings(settings));
    }

    [ReactiveCommand]
    private void Cancel()
    {
        MessageBus.Current.SendMessage(new DetailCloseRequestedMessage());
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
    private Task TestCameraCaptureAsync(CameraConfigViewModel? config)
    {
        if (config == null) return Task.CompletedTask;

        var cameraConfig = new CameraConfig
        {
            Name = config.Name,
            Ip = config.Ip,
            Port = config.Port,
            Channel = config.Channel,
            UserName = config.UserName,
            Password = config.Password
        };

        var row = config;
        _ = RunTestCaptureInBackground(row, cameraConfig);
        return Task.CompletedTask;
    }

    private async Task RunTestCaptureInBackground(CameraConfigViewModel row, CameraConfig cameraConfig)
    {
        try
        {
            var result = await _hikvisionService.TestCaptureAsync(cameraConfig);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (result != null && result.Success)
                {
                    row.LastTestCapturePath = result.Request.SaveFullPath;
                    row.LastTestFailed = false;
                    _logger.LogInformation("测试拍照成功: {Name}, 文件: {Path}", row.Name, result.Request.SaveFullPath);
                }
                else
                {
                    row.LastTestFailed = true;
                    _logger.LogWarning("测试拍照失败: {Name}, {Message}", row.Name, result?.ErrorMessage ?? "无结果");
                }
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                row.LastTestFailed = true;
                _logger.LogError(ex, "测试拍照异常: {Name}", row.Name);
            });
        }
    }

    [ReactiveCommand]
    private void OpenLastTestCapture(CameraConfigViewModel? config)
    {
        if (config == null || string.IsNullOrEmpty(config.LastTestCapturePath)) return;
        var path = config.LastTestCapturePath;
        if (!File.Exists(path))
        {
            _logger.LogWarning("测试拍照文件不存在: {Path}", path);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开测试拍照文件失败: {Path}", path);
        }
    }

    [ReactiveCommand]
    private async Task AddLicensePlateRecognitionAsync()
    {
        var dialogViewModel = new AddLprDialogViewModel(LprDeviceType)
        {
            Name = $"camera_{LicensePlateRecognitionConfigs.Count + 1}",
            Direction = LicensePlateDirection.A
        };

        var dialog = new AddLprDialog(dialogViewModel);
        var window = GetWindow();
        var result = await dialog.ShowDialog<LicensePlateRecognitionConfigViewModel?>(window);

        if (result != null)
        {
            LicensePlateRecognitionConfigs.Add(result);
            SubscribeToLprItemChanges(result);
            RefreshGateIoValidationHints();
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
            Channel = config.Channel,
            EnableGateIo = config.EnableGateIo,
            IoChannel = config.IoChannel
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
                SubscribeToLprItemChanges(result);
                RefreshGateIoValidationHints();
            }
        }
    }

    [ReactiveCommand]
    private void RemoveLicensePlateRecognition(LicensePlateRecognitionConfigViewModel? config)
    {
        if (config != null)
        {
            LicensePlateRecognitionConfigs.Remove(config);
            RefreshGateIoValidationHints();
        }
    }

    [ReactiveCommand]
    private async Task TestLprCaptureAsync(LicensePlateRecognitionConfigViewModel? row)
    {
        if (row == null) return;

        var config = new LicensePlateRecognitionConfig
        {
            Name = row.Name,
            Ip = row.Ip,
            Direction = row.Direction,
            UserName = row.UserName,
            Password = row.Password,
            Port = row.Port,
            Channel = row.Channel,
            EnableGateIo = row.EnableGateIo,
            IoChannel = row.IoChannel
        };

        var device = _lprDeviceResolver.GetDevice(LprDeviceType);
        if (!device.SupportsActiveCapture)
        {
            _logger.LogWarning("当前设备类型不支持主动抓拍: {Type}", LprDeviceType);
            return;
        }

        try
        {
            await device.TriggerCaptureAsync(config);
            _logger.LogInformation("已触发测试抓拍: Device={Device}", config.Name);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "设备不支持主动抓拍: {Device}", config.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试抓拍失败: Device={Device}", config.Name);
        }
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

    [ReactiveCommand]
    private async Task TestDocumentCameraAsync()
    {
        DocumentCameraTestResult = null;

        if (!DocumentCameraEnabled)
        {
            DocumentCameraTestResult = "请先启用高拍仪";
            return;
        }

        if (_usbCameraService == null)
        {
            DocumentCameraTestResult = "高拍仪服务未注册";
            return;
        }

        try
        {
            var isAvailable = await _usbCameraService.IsAvailableAsync();
            if (isAvailable)
            {
                var device = await _usbCameraService.GetFirstAvailableDeviceAsync();
                DocumentCameraTestResult = string.IsNullOrWhiteSpace(device)
                    ? "测试成功：检测到可用设备"
                    : $"测试成功：{device}";
            }
            else
            {
                DocumentCameraTestResult = "测试失败：未检测到可用 USB 摄像头";
            }
        }
        catch (Exception ex)
        {
            DocumentCameraTestResult = $"测试失败: {ex.Message}";
            _logger.LogError(ex, "Document camera test failed");
        }
    }

    [ReactiveCommand]
    private async Task TestSoundDeviceAsync()
    {
        try
        {
            IsSoundDeviceTestRunning = true;
            SoundDeviceTestResult = null;

            await _soundDeviceService.PlayTextV2TestAsync(CancellationToken.None);

            SoundDeviceTestResult = "测试成功";
            _logger.LogInformation("Sound device test succeeded");
        }
        catch (HttpRequestException)
        {
            SoundDeviceTestResult = "测试失败: 网络错误，请检查音响设备IP地址";
            _logger.LogError("Sound device test failed: Network error");
        }
        catch (TaskCanceledException)
        {
            SoundDeviceTestResult = "测试失败: 请求超时，请检查音响设备是否在线";
            _logger.LogError("Sound device test failed: Timeout");
        }
        catch (Exception ex)
        {
            SoundDeviceTestResult = $"测试失败: {ex.Message}";
            _logger.LogError(ex, "Sound device test failed");
        }
        finally
        {
            IsSoundDeviceTestRunning = false;
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

            // Load document camera settings
            DocumentCameraEnabled = settings.SystemSettings.DocumentCameraEnabled;
            DocumentScannerUsbDevice = settings.DocumentScannerConfig.UsbDevice;

            // Load system settings
            EnableAutoStart = settings.SystemSettings.EnableAutoStart;
            EnableChunkedAttachmentUpload = settings.SystemSettings.EnableChunkedAttachmentUpload;
            CaptureStreamType = settings.SystemSettings.CaptureStreamType;
            Urls = settings.SystemSettings.Urls;
            LprDeviceType = settings.SystemSettings.LprDeviceType;
            EnablePrinter = settings.SystemSettings.EnablePrinter;
            SelectedPrinterName = settings.SystemSettings.SelectedPrinterName;
            EnableLatestRecommendation = settings.SystemSettings.EnableLatestRecommendation;
            EnableTriggerLprCapture = settings.SystemSettings.EnableTriggerLprCapture;
            TriggerLprCaptureDelayMs = Math.Max(0, settings.SystemSettings.TriggerLprCaptureDelayMs);
            JpegQuality = settings.SystemSettings.JpegQuality;
            var urbanAnomalyConfig = settings.SystemSettings.UrbanAnomalyDetection ?? new UrbanAnomalyDetectionConfig();
            UrbanAnomalyUpperLimit = urbanAnomalyConfig.UpperLimit;
            UrbanAnomalyLowerLimit = urbanAnomalyConfig.LowerLimit;
            UrbanAnomalyDeviationPercentage = urbanAnomalyConfig.DeviationPercentage;
            var weighingMode = await _settingsService.GetWeighingModeAsync();
            var isUrbanHost =
                weighingMode == WeighingMode.UrbanMode || settings.SystemSettings.IsUrbanMode;
            ShowUrbanAnomalyDetectionSettings = isUrbanHost;
            ShowUrbanConfigSettings = isUrbanHost && _xiaoshanUploadConfigFacade is not null;
            if (isUrbanHost && _xiaoshanUploadConfigFacade is null)
            {
                _logger.LogWarning(
                    "Urban host detected but IXiaoshanUploadConfigClientFacade is not registered; 城管配置 will be hidden");
            }
            DefaultDeliveryType = settings.SystemSettings.DefaultDeliveryType;

            if (ShowUrbanConfigSettings)
            {
                ApplyUrbanConfigFromLocalSettings(settings.UrbanSettings);
                // Server sync is push-on-save only; no pull/refresh in settings UI.
            }

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
            EnablePlateRewrite = settings.WeighingConfiguration.EnablePlateRewrite;
            EnableLatestPlateNumber = settings.WeighingConfiguration.EnableLatestPlateNumber;
            EnableMatchOnStable = settings.WeighingConfiguration.EnableMatchOnStable;

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
                    Channel = config.Channel ?? HikvisionLprDefaults.DefaultChannel,
                    EnableGateIo = config.EnableGateIo,
                    IoChannel = string.IsNullOrWhiteSpace(config.IoChannel) ? "1" : config.IoChannel
                });

            foreach (var item in LicensePlateRecognitionConfigs)
                SubscribeToLprItemChanges(item);
            RefreshGateIoValidationHints();

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

    /// <inheritdoc />
    public void Dispose()
    {
        _lprMessageSubscription?.Dispose();
    }

    private void SubscribeToLprItemChanges(LicensePlateRecognitionConfigViewModel item)
    {
        item.WhenAnyValue(x => x.EnableGateIo, x => x.Direction)
            .Subscribe(_ => RefreshGateIoValidationHints());
    }

    private void RefreshGateIoValidationHints()
    {
        var validation = GateConfigurationValidation.Validate(
            LicensePlateRecognitionConfigs.Select(item => new LicensePlateRecognitionConfig
            {
                Name = item.Name,
                Direction = item.Direction,
                EnableGateIo = item.EnableGateIo
            }));

        foreach (var item in LicensePlateRecognitionConfigs)
        {
            if (!item.EnableGateIo)
            {
                item.GateIoStatusReason = "未启用道闸联动";
                continue;
            }

            item.GateIoStatusReason = validation.IsValid ? "道闸配置有效" : validation.Reason ?? "道闸配置无效";
        }

        if (validation.IsValid)
        {
            GateIoValidationErrorMessage = string.Empty;
            HasGateIoValidationError = false;
        }
        else
        {
            GateIoValidationErrorMessage = validation.Reason ?? "道闸配置无效";
            HasGateIoValidationError = true;
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

    /// <summary>
    ///     Path to the last test capture image for this camera.
    /// </summary>
    [Reactive] private string? _lastTestCapturePath;

    /// <summary>
    ///     Whether the last test capture for this camera failed (used to show "查看" in red).
    /// </summary>
    [Reactive] private bool _lastTestFailed;

    /// <summary>
    ///     Whether this camera has a last test capture to open.
    /// </summary>
    public bool HasLastTestCapture => !string.IsNullOrEmpty(LastTestCapturePath);

    public CameraConfigViewModel()
    {
        this.WhenAnyValue(x => x.LastTestCapturePath)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HasLastTestCapture)));
    }
}

/// <summary>
///     License plate recognition config ViewModel for UI binding
/// </summary>
public partial class LicensePlateRecognitionConfigViewModel : ReactiveObject
{
    [Reactive] private LicensePlateDirection _direction = LicensePlateDirection.A;

    [Reactive] private string _ip = string.Empty;

    [Reactive] private string _name = string.Empty;

    [Reactive] private string? _userName;

    [Reactive] private string? _password;

    [Reactive] private string? _port;

    [Reactive] private string? _channel;

    [Reactive] private bool _enableGateIo;

    [Reactive] private string? _ioChannel;

    [Reactive] private string _gateIoStatusReason = string.Empty;

    /// <summary>
    ///     设备是否在线（由 10 分钟定时检查更新）
    /// </summary>
    [Reactive] private bool _isOnline;

    /// <summary>
    ///     最近一次测试抓拍的车牌号（来自 MessageBus）
    /// </summary>
    [Reactive] private string _lastCapturePlateNumber = string.Empty;

        /// <summary>
        ///     在线状态显示文本
        /// </summary>
        public string OnlineStatusText => IsOnline ? "在线" : "离线";

        /// <summary>
        ///     道闸启用状态显示文本
        /// </summary>
        public string GateIoStatusText => EnableGateIo ? "已关联" : "未关联";

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
        this.WhenAnyValue(x => x.EnableGateIo)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(GateIoStatusText)));
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
    public string DirectionText
    {
        get
        {
            var fieldInfo = Direction.GetType().GetField(Direction.ToString());
            var attribute = fieldInfo?.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault() as DescriptionAttribute;
            return attribute?.Description ?? Direction.ToString();
        }
    }
}