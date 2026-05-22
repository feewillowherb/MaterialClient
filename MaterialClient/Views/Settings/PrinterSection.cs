using Avalonia.Controls;
using MaterialClient.Common.Services;
using MaterialClient.UI.Abstractions;
using MaterialClient.UI.Controls.SettingItems;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views.Settings;

public class PrinterSection : ISettingsSection, ITransientDependency
{
    private readonly ISettingsService _settingsService;

    public PrinterSection(ISettingsService settingsService) => _settingsService = settingsService;

    public string DisplayName => "打印机";
    public bool IsDirty { get; private set; }

    private string _printerName = "";

    public Control CreateView()
    {
        var panel = new StackPanel { Margin = new(0, 0, 0, 16) };
        panel.Children.Add(new TextSettingItem { Label = "打印机名称", Value = _printerName });
        return panel;
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        _printerName = settings.SystemSettings.SelectedPrinterName;
        IsDirty = false;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        settings.SystemSettings.SelectedPrinterName = _printerName;
        await _settingsService.SaveSettingsAsync(settings);
        IsDirty = false;
    }
}
