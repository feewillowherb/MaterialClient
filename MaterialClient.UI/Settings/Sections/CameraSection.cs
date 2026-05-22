using Avalonia.Controls;
using MaterialClient.Common.Services;
using MaterialClient.UI.Abstractions;
using MaterialClient.UI.Controls.SettingItems;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.Settings.Sections;

public class CameraSection : ISettingsSection, ITransientDependency
{
    private readonly ISettingsService _settingsService;

    public CameraSection(ISettingsService settingsService) => _settingsService = settingsService;

    public string DisplayName => "摄像头";
    public bool IsDirty { get; private set; }

    private string _cameraIp = "192.168.1.64";
    private string _cameraPort = "8000";

    public Control CreateView()
    {
        var panel = new StackPanel { Margin = new(0, 0, 0, 16) };
        panel.Children.Add(new TextSettingItem { Label = "摄像头 IP", Value = _cameraIp });
        panel.Children.Add(new TextSettingItem { Label = "端口", Value = _cameraPort });
        return panel;
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        var first = settings.CameraConfigs.FirstOrDefault();
        if (first is not null)
        {
            _cameraIp = first.Ip;
            _cameraPort = first.Port;
        }
        IsDirty = false;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        var first = settings.CameraConfigs.FirstOrDefault();
        if (first is not null)
        {
            first.Ip = _cameraIp;
            first.Port = _cameraPort;
        }
        await _settingsService.SaveSettingsAsync(settings);
        IsDirty = false;
    }
}
