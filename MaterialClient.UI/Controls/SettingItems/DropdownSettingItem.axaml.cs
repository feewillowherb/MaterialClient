using Avalonia;
using Avalonia.Controls;

namespace MaterialClient.UI.Controls.SettingItems;

/// <summary>
///     Dropdown setting item: label + ComboBox bound to a list of options.
/// </summary>
public partial class DropdownSettingItem : UserControl
{
    public DropdownSettingItem()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<DropdownSettingItem, string>(nameof(Label));

    public static readonly StyledProperty<object?> SelectedValueProperty =
        AvaloniaProperty.Register<DropdownSettingItem, object?>(nameof(SelectedValue));

    public static readonly StyledProperty<IEnumerable<object>?> OptionsProperty =
        AvaloniaProperty.Register<DropdownSettingItem, IEnumerable<object>?>(nameof(Options));

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public IEnumerable<object>? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }
}
