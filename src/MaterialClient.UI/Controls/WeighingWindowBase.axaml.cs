using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MaterialClient.UI.Models;

namespace MaterialClient.UI.Controls;

/// <summary>
///     Shared UserControl providing the 4-row chrome layout (header bar, weight area slot,
///     main content slot, status bar) for weighing windows.
///     Host Windows inject content via dependency-property slots and subscribe to
///     routed events for minimize, close, and title-bar drag.
/// </summary>
public partial class WeighingWindowBase : UserControl
{
    public WeighingWindowBase()
    {
        InitializeComponent();
    }

    #region Dependency Properties

    public static readonly StyledProperty<object?> HeaderLeftContentProperty =
        AvaloniaProperty.Register<WeighingWindowBase, object?>(nameof(HeaderLeftContent));

    public static readonly StyledProperty<object?> MenuItemsProperty =
        AvaloniaProperty.Register<WeighingWindowBase, object?>(nameof(MenuItems));

    public static readonly StyledProperty<object?> WeightAreaContentProperty =
        AvaloniaProperty.Register<WeighingWindowBase, object?>(nameof(WeightAreaContent));

    public static readonly StyledProperty<object?> MainContentProperty =
        AvaloniaProperty.Register<WeighingWindowBase, object?>(nameof(MainContent));

    public static readonly StyledProperty<IEnumerable<DeviceStatusItem>?> DeviceStatusesProperty =
        AvaloniaProperty.Register<WeighingWindowBase, IEnumerable<DeviceStatusItem>?>(nameof(DeviceStatuses));

    public static readonly StyledProperty<IEnumerable<CameraStatusDetailItem>?> CameraStatusDetailsProperty =
        AvaloniaProperty.Register<WeighingWindowBase, IEnumerable<CameraStatusDetailItem>?>(nameof(CameraStatusDetails));

    public object? HeaderLeftContent
    {
        get => GetValue(HeaderLeftContentProperty);
        set => SetValue(HeaderLeftContentProperty, value);
    }

    public object? MenuItems
    {
        get => GetValue(MenuItemsProperty);
        set => SetValue(MenuItemsProperty, value);
    }

    public object? WeightAreaContent
    {
        get => GetValue(WeightAreaContentProperty);
        set => SetValue(WeightAreaContentProperty, value);
    }

    public object? MainContent
    {
        get => GetValue(MainContentProperty);
        set => SetValue(MainContentProperty, value);
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

    #endregion

    #region Routed Events

    public static readonly RoutedEvent<RoutedEventArgs> MinimizeButtonClickEvent =
        RoutedEvent.Register<WeighingWindowBase, RoutedEventArgs>(
            nameof(MinimizeButtonClick), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> CloseButtonClickEvent =
        RoutedEvent.Register<WeighingWindowBase, RoutedEventArgs>(
            nameof(CloseButtonClick), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> TitleBarPointerPressedEvent =
        RoutedEvent.Register<WeighingWindowBase, RoutedEventArgs>(
            nameof(TitleBarPointerPressed), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs> MinimizeButtonClick
    {
        add => AddHandler(MinimizeButtonClickEvent, value);
        remove => RemoveHandler(MinimizeButtonClickEvent, value);
    }

    public event EventHandler<RoutedEventArgs> CloseButtonClick
    {
        add => AddHandler(CloseButtonClickEvent, value);
        remove => RemoveHandler(CloseButtonClickEvent, value);
    }

    public event EventHandler<RoutedEventArgs> TitleBarPointerPressed
    {
        add => AddHandler(TitleBarPointerPressedEvent, value);
        remove => RemoveHandler(TitleBarPointerPressedEvent, value);
    }

    #endregion

    #region Internal Event Handlers

    private void OnMinimizeButtonClick(object? sender, RoutedEventArgs e)
        => RaiseEvent(new RoutedEventArgs(MinimizeButtonClickEvent));

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
        => RaiseEvent(new RoutedEventArgs(CloseButtonClickEvent));

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        RaiseEvent(new TitleBarPointerPressedRoutedEventArgs(TitleBarPointerPressedEvent, e));
    }

    #endregion

    #region Nested Types

    /// <summary>
    ///     Routed event args that carry the original <see cref="PointerPressedEventArgs" />
    ///     so the host Window can call <c>BeginMoveDrag</c>.
    /// </summary>
    public class TitleBarPointerPressedRoutedEventArgs : RoutedEventArgs
    {
        public PointerPressedEventArgs PointerArgs { get; }

        public TitleBarPointerPressedRoutedEventArgs(
            RoutedEvent routedEvent,
            PointerPressedEventArgs pointerArgs) : base(routedEvent)
        {
            PointerArgs = pointerArgs;
        }
    }

    #endregion
}
