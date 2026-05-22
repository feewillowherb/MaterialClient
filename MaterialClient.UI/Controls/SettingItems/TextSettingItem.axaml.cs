using Avalonia;
using Avalonia.Controls;

namespace MaterialClient.UI.Controls.SettingItems;

/// <summary>
///     Text setting item: label + TextBox bound to a string value.
/// </summary>
public partial class TextSettingItem : UserControl
{
    public TextSettingItem()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<TextSettingItem, string>(nameof(Label));

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<TextSettingItem, string>(nameof(Value));

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
}
