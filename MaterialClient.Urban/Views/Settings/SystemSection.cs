using Avalonia.Controls;
using MaterialClient.Common.Services;
using MaterialClient.UI.Abstractions;
using MaterialClient.UI.Controls.SettingItems;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Views.Settings;

/// <summary>
///     Urban system settings section: auto-start toggle and general settings.
/// </summary>
public class SystemSection : ISettingsSection, ITransientDependency
{
    private readonly ISettingsService _settingsService;

    public SystemSection(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public string DisplayName => "系统";
    public bool IsDirty { get; private set; }

    private bool _enableAutoStart;

    public Control CreateView()
    {
        var panel = new StackPanel { Margin = new(0, 0, 0, 16) };

        var autoStartItem = new ToggleSettingItem
        {
            Label = "开机自启动",
            Value = _enableAutoStart,
        };

        panel.Children.Add(autoStartItem);

        return panel;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        _enableAutoStart = settings.SystemSettings.EnableAutoStart;
        IsDirty = false;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        settings.SystemSettings.EnableAutoStart = _enableAutoStart;
        await _settingsService.SaveSettingsAsync(settings);
        IsDirty = false;
    }
}
