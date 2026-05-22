using Avalonia.Controls;
using MaterialClient.Common.Services;
using MaterialClient.UI.Abstractions;
using MaterialClient.UI.Controls.SettingItems;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views.Settings;

public class SoundDeviceSection : ISettingsSection, ITransientDependency
{
    private readonly ISettingsService _settingsService;

    public SoundDeviceSection(ISettingsService settingsService) => _settingsService = settingsService;

    public string DisplayName => "音频设备";
    public bool IsDirty { get; private set; }

    private string _soundIp = "";
    private bool _enabled;

    public Control CreateView()
    {
        var panel = new StackPanel { Margin = new(0, 0, 0, 16) };
        panel.Children.Add(new ToggleSettingItem { Label = "启用音柱", Value = _enabled });
        panel.Children.Add(new TextSettingItem { Label = "音柱 IP", Value = _soundIp });
        return panel;
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        _soundIp = settings.SoundDeviceSettings.SoundIP;
        _enabled = settings.SoundDeviceSettings.Enabled;
        IsDirty = false;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        settings.SoundDeviceSettings.SoundIP = _soundIp;
        settings.SoundDeviceSettings.Enabled = _enabled;
        await _settingsService.SaveSettingsAsync(settings);
        IsDirty = false;
    }
}
