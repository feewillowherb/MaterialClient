using System;
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
    private readonly LprDeviceType _lprDeviceType;

    [Reactive] private string _name = string.Empty;
    [Reactive] private string _ip = string.Empty;
    [Reactive] private LicensePlateDirection _direction = LicensePlateDirection.A;
    [Reactive] private string? _userName;
    [Reactive] private string? _password;
    [Reactive] private string? _port;
    [Reactive] private string? _channel;
    [Reactive] private bool _enableGateIo;
    [Reactive] private string? _ioChannel;

    /// <summary>
    ///     是否显示海康威视专用配置字段（含通道）
    /// </summary>
    public bool ShowHikvisionLprFields => _lprDeviceType == LprDeviceType.Hikvision;

    /// <summary>
    ///     是否显示臻识 Vz SDK 连接字段（用户名、密码、端口，无通道）
    /// </summary>
    public bool ShowVzvisionLprFields => _lprDeviceType == LprDeviceType.Vzvision;

    public AddLprDialogViewModel(LprDeviceType lprDeviceType = LprDeviceType.Hikvision)
    {
        _lprDeviceType = lprDeviceType;

        // 唯一数据源：海康威视时 UI 默认显示与保存时使用的默认值一致
        if (HikvisionLprDefaults.ShouldApply(lprDeviceType))
        {
            _userName = HikvisionLprDefaults.DefaultUserName;
            _port = HikvisionLprDefaults.DefaultPort;
            _channel = HikvisionLprDefaults.DefaultChannel;
        }
        else if (VzvisionLprDefaults.ShouldApply(lprDeviceType))
        {
            _userName = VzvisionLprDefaults.DefaultUserName;
            _port = VzvisionLprDefaults.DefaultPort;
        }

        _ioChannel ??= "1";

        this.WhenAnyValue(x => x.Direction)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(DirectionIndex));
                this.RaisePropertyChanged(nameof(DirectionText));
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

    [ReactiveCommand]
    private void Save()
    {
        string? userName = UserName;
        string? password = Password;
        string? port = Port;
        string? channel = Channel;
        string? ioChannel = IoChannel;

        if (HikvisionLprDefaults.ShouldApply(_lprDeviceType))
        {
            if (string.IsNullOrWhiteSpace(userName))
                userName = HikvisionLprDefaults.DefaultUserName;
            if (string.IsNullOrWhiteSpace(port))
                port = HikvisionLprDefaults.DefaultPort;
            if (string.IsNullOrWhiteSpace(channel))
                channel = HikvisionLprDefaults.DefaultChannel;
            if (password == null)
                password = string.Empty;
        }
        else if (VzvisionLprDefaults.ShouldApply(_lprDeviceType))
        {
            if (string.IsNullOrWhiteSpace(userName))
                userName = VzvisionLprDefaults.DefaultUserName;
            if (string.IsNullOrWhiteSpace(port))
                port = VzvisionLprDefaults.DefaultPort;
            if (password == null)
                password = string.Empty;
            channel = null;
        }

        Result = new LicensePlateRecognitionConfigViewModel
        {
            Name = Name,
            Ip = Ip,
            Direction = Direction,
            UserName = userName,
            Password = password,
            Port = port,
            Channel = _lprDeviceType == LprDeviceType.Hikvision
                ? (channel ?? HikvisionLprDefaults.DefaultChannel)
                : null,
            EnableGateIo = EnableGateIo,
            IoChannel = string.IsNullOrWhiteSpace(ioChannel) ? "1" : ioChannel
        };
    }

    [ReactiveCommand]
    private void Cancel()
    {
        Result = null;
    }
}
