using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using MaterialClient.UI.Models;

namespace MaterialClient.UI.Controls;

public partial class WeighingWindowBase : UserControl
{
    public static readonly StyledProperty<object?> LogoContentProperty =
        AvaloniaProperty.Register<WeighingWindowBase, object?>(nameof(LogoContent));

    public static readonly StyledProperty<object?> MenuContentProperty =
        AvaloniaProperty.Register<WeighingWindowBase, object?>(nameof(MenuContent));

    public static readonly StyledProperty<object?> WeightContentProperty =
        AvaloniaProperty.Register<WeighingWindowBase, object?>(nameof(WeightContent));

    public static readonly StyledProperty<object?> ContentAreaProperty =
        AvaloniaProperty.Register<WeighingWindowBase, object?>(nameof(ContentArea));

    public static readonly StyledProperty<bool> ShowWeightDisplayProperty =
        AvaloniaProperty.Register<WeighingWindowBase, bool>(nameof(ShowWeightDisplay), true);

    public static readonly StyledProperty<bool> CanMinimizeProperty =
        AvaloniaProperty.Register<WeighingWindowBase, bool>(nameof(CanMinimize), true);

    public static readonly StyledProperty<bool> CanCloseProperty =
        AvaloniaProperty.Register<WeighingWindowBase, bool>(nameof(CanClose), true);

    public static readonly StyledProperty<IEnumerable<DeviceStatusItem>?> DeviceStatusesProperty =
        AvaloniaProperty.Register<WeighingWindowBase, IEnumerable<DeviceStatusItem>?>(nameof(DeviceStatuses));

    public static readonly StyledProperty<IEnumerable<CameraStatusDetailItem>?> CameraStatusDetailsProperty =
        AvaloniaProperty.Register<WeighingWindowBase, IEnumerable<CameraStatusDetailItem>?>(
            nameof(CameraStatusDetails));

    public static readonly StyledProperty<IBrush?> StatusBarBackgroundProperty =
        AvaloniaProperty.Register<WeighingWindowBase, IBrush?>(nameof(StatusBarBackground), Brushes.WhiteSmoke);

    public static readonly StyledProperty<IBrush?> StatusBarBorderBrushProperty =
        AvaloniaProperty.Register<WeighingWindowBase, IBrush?>(nameof(StatusBarBorderBrush), Brushes.LightGray);

    public static readonly StyledProperty<Thickness> StatusBarPaddingProperty =
        AvaloniaProperty.Register<WeighingWindowBase, Thickness>(nameof(StatusBarPadding), new Thickness(16, 8));

    public WeighingWindowBase()
    {
        InitializeComponent();
    }

    public object? LogoContent
    {
        get => GetValue(LogoContentProperty);
        set => SetValue(LogoContentProperty, value);
    }

    public object? MenuContent
    {
        get => GetValue(MenuContentProperty);
        set => SetValue(MenuContentProperty, value);
    }

    public object? WeightContent
    {
        get => GetValue(WeightContentProperty);
        set => SetValue(WeightContentProperty, value);
    }

    public object? ContentArea
    {
        get => GetValue(ContentAreaProperty);
        set => SetValue(ContentAreaProperty, value);
    }

    public bool ShowWeightDisplay
    {
        get => GetValue(ShowWeightDisplayProperty);
        set => SetValue(ShowWeightDisplayProperty, value);
    }

    public bool CanMinimize
    {
        get => GetValue(CanMinimizeProperty);
        set => SetValue(CanMinimizeProperty, value);
    }

    public bool CanClose
    {
        get => GetValue(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }

    public IEnumerable<DeviceStatusItem>? DeviceStatuses
    {
        get => GetValue(DeviceStatusesProperty);
        set => SetValue(DeviceStatusesProperty, value);
    }

    public IEnumerable<CameraStatusDetailItem>? CameraStatusDetails
    {
        get => GetValue(CameraStatusDetailsProperty);
        set => SetValue(CameraStatusDetailsProperty, value);
    }

    public IBrush? StatusBarBackground
    {
        get => GetValue(StatusBarBackgroundProperty);
        set => SetValue(StatusBarBackgroundProperty, value);
    }

    public IBrush? StatusBarBorderBrush
    {
        get => GetValue(StatusBarBorderBrushProperty);
        set => SetValue(StatusBarBorderBrushProperty, value);
    }

    public Thickness StatusBarPadding
    {
        get => GetValue(StatusBarPaddingProperty);
        set => SetValue(StatusBarPaddingProperty, value);
    }

    public event EventHandler<PointerPressedEventArgs>? TitleBarPointerPressed;
    public event EventHandler<RoutedEventArgs>? MinimizeButtonClick;
    public event EventHandler<RoutedEventArgs>? CloseButtonClick;

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e) =>
        TitleBarPointerPressed?.Invoke(this, e);

    private void OnMinimizeButtonClick(object? sender, RoutedEventArgs e) =>
        MinimizeButtonClick?.Invoke(this, e);

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e) =>
        CloseButtonClick?.Invoke(this, e);
}
