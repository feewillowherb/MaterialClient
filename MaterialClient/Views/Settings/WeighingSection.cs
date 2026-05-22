using Avalonia.Controls;
using MaterialClient.Common.Services;
using MaterialClient.UI.Abstractions;
using MaterialClient.UI.Controls.SettingItems;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views.Settings;

public class WeighingSection : ISettingsSection, ITransientDependency
{
    private readonly ISettingsService _settingsService;

    public WeighingSection(ISettingsService settingsService) => _settingsService = settingsService;

    public string DisplayName => "称重设置";
    public bool IsDirty { get; private set; }

    private double _minWeightThreshold;
    private double _stabilityWindowMs;

    public Control CreateView()
    {
        var panel = new StackPanel { Margin = new(0, 0, 0, 16) };
        panel.Children.Add(new SliderSettingItem
        {
            Label = "最小重量阈值（吨）",
            Minimum = 0,
            Maximum = 50,
            Step = 0.5,
            Value = (double)_minWeightThreshold,
        });
        panel.Children.Add(new SliderSettingItem
        {
            Label = "稳定窗口（毫秒）",
            Minimum = 100,
            Maximum = 10000,
            Step = 100,
            Value = _stabilityWindowMs,
        });
        return panel;
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        _minWeightThreshold = (double)settings.WeighingConfiguration.MinWeightThreshold;
        _stabilityWindowMs = settings.WeighingConfiguration.StabilityWindowMs;
        IsDirty = false;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        settings.WeighingConfiguration.MinWeightThreshold = (decimal)_minWeightThreshold;
        settings.WeighingConfiguration.StabilityWindowMs = (int)_stabilityWindowMs;
        await _settingsService.SaveSettingsAsync(settings);
        IsDirty = false;
    }
}
