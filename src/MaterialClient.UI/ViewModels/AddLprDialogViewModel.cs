using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.UI.ViewModels;

/// <summary>
///     ViewModel for Add LPR Dialog
/// </summary>
public partial class AddLprDialogViewModel : ViewModelBase
{
    [Reactive] private LprDeviceType _deviceType = LprDeviceType.Hikvision;
    [Reactive] private LprSiteType _siteType = LprSiteType.Scale;
    [Reactive] private string _name = string.Empty;
    [Reactive] private string _ip = string.Empty;
    [Reactive] private LicensePlateDirection _direction = LicensePlateDirection.A;
    [Reactive] private string? _userName;
    [Reactive] private string? _password;
    [Reactive] private string? _port;
    [Reactive] private string? _channel;
    [Reactive] private bool _enableGateIo;
    [Reactive] private string? _ioChannel;

    public ObservableCollection<LprSiteType> LprSiteTypeOptions { get; } =
    [
        LprSiteType.Scale,
        LprSiteType.Checkpoint,
        LprSiteType.FinishedProduct
    ];

    public bool CanEditSiteType { get; }

    public ObservableCollection<LprDeviceType> LprDeviceTypeOptions { get; } =
    [
        LprDeviceType.Hikvision,
        LprDeviceType.Vzvision,
        LprDeviceType.Huaxiazhixin
    ];

    public bool ShowHikvisionLprFields => DeviceType == LprDeviceType.Hikvision;

    public bool ShowVzvisionLprFields => DeviceType == LprDeviceType.Vzvision;

    public bool ShowGateIoFields => DeviceType == LprDeviceType.Vzvision;

    public bool IsEditMode { get; }

    public string DialogTitle => IsEditMode ? "编辑车牌识别设备" : "添加车牌识别设备";

    public AddLprDialogViewModel(
        LprDeviceType deviceType = LprDeviceType.Hikvision,
        bool isEditMode = false,
        bool canEditSiteType = false)
    {
        IsEditMode = isEditMode;
        CanEditSiteType = canEditSiteType;
        _deviceType = deviceType;
        if (!canEditSiteType)
            _siteType = LprSiteType.Scale;
        ApplyEmptyVendorDefaults(deviceType);
        _ioChannel ??= "1";

        this.WhenAnyValue(x => x.Direction)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(DirectionIndex));
                this.RaisePropertyChanged(nameof(DirectionText));
            });

        this.WhenAnyValue(x => x.DeviceType)
            .Subscribe(type =>
            {
                this.RaisePropertyChanged(nameof(ShowHikvisionLprFields));
                this.RaisePropertyChanged(nameof(ShowVzvisionLprFields));
                this.RaisePropertyChanged(nameof(ShowGateIoFields));
                ApplyEmptyVendorDefaults(type);
            });
    }

    public int DirectionIndex
    {
        get => (int)_direction;
        set
        {
            if (value is >= 0 and <= 1)
            {
                Direction = (LicensePlateDirection)value;
                this.RaisePropertyChanged();
            }
        }
    }

    public string DirectionText
    {
        get
        {
            var fieldInfo = _direction.GetType().GetField(_direction.ToString());
            var attribute = fieldInfo?.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault() as DescriptionAttribute;
            return attribute?.Description ?? _direction.ToString();
        }
    }

    public LicensePlateRecognitionConfigViewModel? Result { get; private set; }

    public AddLprDialogViewModel() : this(LprDeviceType.Hikvision)
    {
    }

    public void ApplyRow(LicensePlateRecognitionConfigViewModel row)
    {
        Name = row.Name;
        Ip = row.Ip;
        Direction = row.Direction;
        UserName = row.UserName;
        Password = row.Password;
        Port = row.Port;
        Channel = row.Channel;
        EnableGateIo = row.EnableGateIo;
        IoChannel = row.IoChannel;
        DeviceType = row.DeviceType;
        SiteType = CanEditSiteType ? row.SiteType : LprSiteType.Scale;
    }

    private void ApplyEmptyVendorDefaults(LprDeviceType deviceType)
    {
        if (HikvisionLprDefaults.ShouldApply(deviceType))
        {
            if (string.IsNullOrWhiteSpace(_userName))
                UserName = HikvisionLprDefaults.DefaultUserName;
            if (string.IsNullOrWhiteSpace(_port))
                Port = HikvisionLprDefaults.DefaultPort;
            if (string.IsNullOrWhiteSpace(_channel))
                Channel = HikvisionLprDefaults.DefaultChannel;
        }
        else if (VzvisionLprDefaults.ShouldApply(deviceType))
        {
            if (string.IsNullOrWhiteSpace(_userName))
                UserName = VzvisionLprDefaults.DefaultUserName;
            if (string.IsNullOrWhiteSpace(_port))
                Port = VzvisionLprDefaults.DefaultPort;
        }
    }

    [ReactiveCommand]
    private void Save()
    {
        string? userName = UserName;
        string? password = Password;
        string? port = Port;
        string? channel = Channel;
        string? ioChannel = IoChannel;

        var config = LicensePlateRecognitionConfig.FromUi(
            Name,
            Ip,
            Direction,
            userName,
            password,
            port,
            channel,
            EnableGateIo && DeviceType == LprDeviceType.Vzvision,
            ioChannel,
            DeviceType,
            CanEditSiteType ? SiteType : LprSiteType.Scale);

        Result = LicensePlateRecognitionConfigViewModel.FromConfig(config);
    }

    [ReactiveCommand]
    private void Cancel()
    {
        Result = null;
    }
}
