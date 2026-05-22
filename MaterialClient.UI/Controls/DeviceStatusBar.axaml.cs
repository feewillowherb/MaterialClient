using Avalonia;
using Avalonia.Controls;
using MaterialClient.UI.Models;

namespace MaterialClient.UI.Controls;

/// <summary>
///     Reusable device status bar control displaying online/offline indicators.
///     Binds ItemsSource to a collection of DeviceStatusItem records.
/// </summary>
public partial class DeviceStatusBar : UserControl
{
    public DeviceStatusBar()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     The collection of device status items to display.
    /// </summary>
    public static readonly StyledProperty<IEnumerable<DeviceStatusItem>?> ItemsSourceProperty =
        AvaloniaProperty.Register<DeviceStatusBar, IEnumerable<DeviceStatusItem>?>(nameof(ItemsSource));

    public IEnumerable<DeviceStatusItem>? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty)
        {
            var itemsControl = this.FindControl<ItemsControl>("PART_ItemsControl");
            if (itemsControl is not null)
            {
                itemsControl.ItemsSource = change.NewValue as IEnumerable<DeviceStatusItem>;
            }
        }
    }
}
