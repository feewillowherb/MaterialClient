using Avalonia;
using Avalonia.Controls;

namespace MaterialClient.UI.Controls.SettingItems;

/// <summary>
///     Toggle switch setting item: label + ToggleSwitch bound to a boolean value.
/// </summary>
public partial class ToggleSettingItem : UserControl
{
    public ToggleSettingItem()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ToggleSettingItem, string>(nameof(Label));

    public static readonly StyledProperty<bool> ValueProperty =
        AvaloniaProperty.Register<ToggleSettingItem, bool>(nameof(Value));

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
}
