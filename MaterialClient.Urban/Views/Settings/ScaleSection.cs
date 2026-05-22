using Avalonia.Controls;
using MaterialClient.Common.Services;
using MaterialClient.UI.Abstractions;
using MaterialClient.UI.Controls.SettingItems;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Views.Settings;

/// <summary>
///     Urban scale settings section: serial port, baud rate.
/// </summary>
public class ScaleSection : ISettingsSection, ITransientDependency
{
    private readonly ISettingsService _settingsService;

    public ScaleSection(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public string DisplayName => "地磅设置";
    public bool IsDirty { get; private set; }

    private string _serialPort = "COM1";
    private string _baudRate = "9600";

    public Control CreateView()
    {
        var panel = new StackPanel { Margin = new(0, 0, 0, 16) };

        var serialPortItem = new DropdownSettingItem
        {
            Label = "地磅串口",
            Options = ["COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8"],
            SelectedValue = _serialPort,
        };

        var baudRateItem = new DropdownSettingItem
        {
            Label = "波特率",
            Options = ["9600", "19200", "38400", "57600", "115200"],
            SelectedValue = _baudRate,
        };

        panel.Children.Add(serialPortItem);
        panel.Children.Add(baudRateItem);

        return panel;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        _serialPort = settings.ScaleSettings.SerialPort;
        _baudRate = settings.ScaleSettings.BaudRate;
        IsDirty = false;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync();
        settings.ScaleSettings.SerialPort = _serialPort;
        settings.ScaleSettings.BaudRate = _baudRate;
        await _settingsService.SaveSettingsAsync(settings);
        IsDirty = false;
    }
}
