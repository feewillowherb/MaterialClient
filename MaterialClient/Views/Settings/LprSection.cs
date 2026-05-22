using Avalonia.Controls;
using MaterialClient.Common.Services;
using MaterialClient.UI.Abstractions;
using MaterialClient.UI.Controls.SettingItems;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views.Settings;

public class LprSection : ISettingsSection, ITransientDependency
{
    private readonly ISettingsService _settingsService;

    public LprSection(ISettingsService settingsService) => _settingsService = settingsService;

    public string DisplayName => "车牌识别";
    public bool IsDirty { get; private set; }

    private string _lprIp = "192.168.1.64";
    private double _jpegQuality = 90;

    public Control CreateView()
    {
        var panel = new StackPanel { Margin = new(0, 0, 0, 16) };
        panel.Children.Add(new TextSettingItem { Label = "设备 IP", Value = _lprIp });
        panel.Children.Add(new SliderSettingItem
        {
            Label = "JPEG 压缩质量",
            Minimum = 50,
            Maximum = 100,
            Step = 5,
            Value = _jpegQuality,
        });
        return panel;
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        var first = settings.LicensePlateRecognitionConfigs.FirstOrDefault();
        if (first is not null) _lprIp = first.Ip;
        _jpegQuality = settings.SystemSettings.JpegQuality;
        IsDirty = false;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        var first = settings.LicensePlateRecognitionConfigs.FirstOrDefault();
        if (first is not null) first.Ip = _lprIp;
        settings.SystemSettings.JpegQuality = (int)_jpegQuality;
        await _settingsService.SaveSettingsAsync(settings);
        IsDirty = false;
    }
}
