using Avalonia.Controls;
using MaterialClient.Common.Services;
using MaterialClient.UI.Abstractions;
using MaterialClient.UI.Controls.SettingItems;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Views.Settings;

/// <summary>
///     Urban camera settings section: camera device configuration using CameraConfig list.
/// </summary>
public class CameraSection : ISettingsSection, ITransientDependency
{
    private readonly ISettingsService _settingsService;

    public CameraSection(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public string DisplayName => "摄像头";
    public bool IsDirty { get; private set; }

    private string _cameraIp = "192.168.1.64";
    private string _cameraPort = "8000";

    public Control CreateView()
    {
        var panel = new StackPanel { Margin = new(0, 0, 0, 16) };

        var ipItem = new TextSettingItem
        {
            Label = "摄像头 IP",
            Value = _cameraIp,
        };

        var portItem = new TextSettingItem
        {
            Label = "端口",
            Value = _cameraPort,
        };

        panel.Children.Add(ipItem);
        panel.Children.Add(portItem);

        return panel;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        var firstCamera = settings.CameraConfigs.FirstOrDefault();
        if (firstCamera is not null)
        {
            _cameraIp = firstCamera.Ip;
            _cameraPort = firstCamera.Port;
        }
        IsDirty = false;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        var firstCamera = settings.CameraConfigs.FirstOrDefault();
        if (firstCamera is not null)
        {
            firstCamera.Ip = _cameraIp;
            firstCamera.Port = _cameraPort;
        }
        await _settingsService.SaveSettingsAsync(settings);
        IsDirty = false;
    }
}
