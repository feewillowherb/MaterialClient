using Avalonia.Controls;
using MaterialClient.Common.Services;
using MaterialClient.UI.Abstractions;
using MaterialClient.UI.Controls.SettingItems;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views.Settings;

public class SystemSection : ISettingsSection, ITransientDependency
{
    private readonly ISettingsService _settingsService;

    public SystemSection(ISettingsService settingsService) => _settingsService = settingsService;

    public string DisplayName => "系统";
    public bool IsDirty { get; private set; }

    private bool _enableAutoStart;
    private bool _enablePrinter;

    public Control CreateView()
    {
        var panel = new StackPanel { Margin = new(0, 0, 0, 16) };
        panel.Children.Add(new ToggleSettingItem { Label = "开机自启动", Value = _enableAutoStart });
        panel.Children.Add(new ToggleSettingItem { Label = "启用打印机", Value = _enablePrinter });
        return panel;
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        _enableAutoStart = settings.SystemSettings.EnableAutoStart;
        _enablePrinter = settings.SystemSettings.EnablePrinter;
        IsDirty = false;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        settings.SystemSettings.EnableAutoStart = _enableAutoStart;
        settings.SystemSettings.EnablePrinter = _enablePrinter;
        await _settingsService.SaveSettingsAsync(settings);
        IsDirty = false;
    }
}
