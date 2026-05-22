using Avalonia.Controls;
using MaterialClient.Common.Services;
using MaterialClient.UI.Abstractions;
using MaterialClient.UI.Controls.SettingItems;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Views.Settings;

/// <summary>
///     Urban LPR settings section: LPR device configuration and JPEG quality slider.
/// </summary>
public class LprSection : ISettingsSection, ITransientDependency
{
    private readonly ISettingsService _settingsService;

    public LprSection(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public string DisplayName => "车牌识别";
    public bool IsDirty { get; private set; }

    private string _lprDeviceIp = "192.168.1.64";
    private double _jpegQuality = 90;

    public Control CreateView()
    {
        var panel = new StackPanel { Margin = new(0, 0, 0, 16) };

        var ipItem = new TextSettingItem
        {
            Label = "设备 IP",
            Value = _lprDeviceIp,
        };

        var qualityItem = new SliderSettingItem
        {
            Label = "JPEG 压缩质量",
            Minimum = 50,
            Maximum = 100,
            Step = 5,
            Value = _jpegQuality,
        };

        panel.Children.Add(ipItem);
        panel.Children.Add(qualityItem);

        return panel;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        var firstLpr = settings.LicensePlateRecognitionConfigs.FirstOrDefault();
        if (firstLpr is not null)
        {
            _lprDeviceIp = firstLpr.Ip;
        }
        _jpegQuality = settings.SystemSettings.JpegQuality;
        IsDirty = false;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        var firstLpr = settings.LicensePlateRecognitionConfigs.FirstOrDefault();
        if (firstLpr is not null)
        {
            firstLpr.Ip = _lprDeviceIp;
        }
        settings.SystemSettings.JpegQuality = (int)_jpegQuality;
        await _settingsService.SaveSettingsAsync(settings);
        IsDirty = false;
    }
}
