using Avalonia;
using Avalonia.Controls;

namespace MaterialClient.UI.Controls.SettingItems;

/// <summary>
///     Slider setting item: label + Slider + value display, supports Min/Max/Step.
/// </summary>
public partial class SliderSettingItem : UserControl
{
    public SliderSettingItem()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SliderSettingItem, string>(nameof(Label));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<SliderSettingItem, double>(nameof(Value));

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<SliderSettingItem, double>(nameof(Minimum), 0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<SliderSettingItem, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<SliderSettingItem, double>(nameof(Step), 1);

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }
}
