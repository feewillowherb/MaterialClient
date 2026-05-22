using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using MaterialClient.UI.Models;

namespace MaterialClient.UI.Controls;

/// <summary>
///     Reusable device status bar with optional camera hover detail popup.
/// </summary>
public partial class DeviceStatusBar : UserControl
{
    private CancellationTokenSource? _closePopupCts;
    private bool _isMouseOverPopup;
    private Control? _cameraItemHost;

    public DeviceStatusBar()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<IEnumerable<DeviceStatusItem>?> ItemsSourceProperty =
        AvaloniaProperty.Register<DeviceStatusBar, IEnumerable<DeviceStatusItem>?>(nameof(ItemsSource));

    public static readonly StyledProperty<IEnumerable<CameraStatusDetailItem>?> CameraStatusDetailsProperty =
        AvaloniaProperty.Register<DeviceStatusBar, IEnumerable<CameraStatusDetailItem>?>(nameof(CameraStatusDetails));

    public IEnumerable<DeviceStatusItem>? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IEnumerable<CameraStatusDetailItem>? CameraStatusDetails
    {
        get => GetValue(CameraStatusDetailsProperty);
        set => SetValue(CameraStatusDetailsProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyItemsSource(ItemsSource);
        WireItemsControl();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnwireItemsControl();
        _closePopupCts?.Cancel();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty)
        {
            ApplyItemsSource(change.NewValue as IEnumerable<DeviceStatusItem>);
        }
    }

    private ItemsControl? GetItemsControl() => this.FindControl<ItemsControl>("PART_ItemsControl");

    private Popup? GetCameraPopup() => this.FindControl<Popup>("CameraDetailPopup");

    private void WireItemsControl()
    {
        var itemsControl = GetItemsControl();
        if (itemsControl is null) return;

        itemsControl.ContainerPrepared += OnContainerPrepared;
        itemsControl.ContainerClearing += OnContainerClearing;
    }

    private void UnwireItemsControl()
    {
        var itemsControl = GetItemsControl();
        if (itemsControl is null) return;

        itemsControl.ContainerPrepared -= OnContainerPrepared;
        itemsControl.ContainerClearing -= OnContainerClearing;
        DetachCameraHost(_cameraItemHost);
        _cameraItemHost = null;
    }

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container.DataContext is not DeviceStatusItem item) return;
        if (item.Name != DeviceStatusCatalog.CameraName) return;

        _cameraItemHost = e.Container;
        e.Container.PointerEntered += OnCameraItemPointerEntered;
        e.Container.PointerExited += OnCameraItemPointerExited;

        var popup = GetCameraPopup();
        if (popup is not null)
        {
            popup.PlacementTarget = e.Container;
        }
    }

    private void OnContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        if (e.Container != _cameraItemHost) return;
        DetachCameraHost(_cameraItemHost);
        _cameraItemHost = null;
    }

    private void DetachCameraHost(Control? host)
    {
        if (host is null) return;
        host.PointerEntered -= OnCameraItemPointerEntered;
        host.PointerExited -= OnCameraItemPointerExited;
    }

    private void OnCameraItemPointerEntered(object? sender, PointerEventArgs e)
    {
        _closePopupCts?.Cancel();
        _closePopupCts = null;

        var popup = GetCameraPopup();
        if (popup is not null) popup.IsOpen = true;
    }

    private async void OnCameraItemPointerExited(object? sender, PointerEventArgs e)
    {
        var popup = GetCameraPopup();
        if (popup?.IsOpen != true || _isMouseOverPopup) return;

        _closePopupCts?.Cancel();
        _closePopupCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(150, _closePopupCts.Token);
            if (!_isMouseOverPopup && popup is not null) popup.IsOpen = false;
        }
        catch (TaskCanceledException)
        {
            // Mouse moved onto popup
        }
    }

    private void CameraDetailPopup_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _isMouseOverPopup = true;
        _closePopupCts?.Cancel();
        _closePopupCts = null;
    }

    private async void CameraDetailPopup_OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isMouseOverPopup = false;

        _closePopupCts?.Cancel();
        _closePopupCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(150, _closePopupCts.Token);
            if (!_isMouseOverPopup && GetCameraPopup() is { } popup) popup.IsOpen = false;
        }
        catch (TaskCanceledException)
        {
            // Mouse moved back to host
        }
    }

    private void ApplyItemsSource(IEnumerable<DeviceStatusItem>? items)
    {
        var itemsControl = GetItemsControl();
        if (itemsControl is not null)
        {
            itemsControl.ItemsSource = items;
        }
    }
}
