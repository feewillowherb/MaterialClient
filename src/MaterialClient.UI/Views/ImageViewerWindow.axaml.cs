using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MaterialClient.UI.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.Views;

public partial class ImageViewerWindow : Window, ITransientDependency
{
    private const double ImageMargin = 40; // 20 each side
    private const double ZoomMin = 0.5;
    private const double ZoomMax = 4.0;
    private const double ZoomStep = 0.12;

    private readonly IDisposable? _closeSubscription;
    private bool _isMaximized;
    private PixelPoint _previousPosition;
    private Size _previousSize;

    private double _zoom = 1.0;
    private double _baseContentWidth = double.NaN;
    private double _baseContentHeight = double.NaN;

    private bool _isPanning;
    private Point _lastPointerPositionForPan;

    public ImageViewerWindow(ImageViewerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        if (viewModel.CloseCommand != null)
            _closeSubscription = viewModel.CloseCommand.Subscribe(_ => Close());

        KeyDown += OnKeyDown;
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateBaseSizeFromBounds, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void MaximizeButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ImageArea_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ImageArea_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var scrollViewer = this.FindControl<ScrollViewer>("ImageScrollViewer");
        var grid = this.FindControl<Grid>("ImageContainerGrid");
        var image = this.FindControl<Image>("ImageControl");
        if (scrollViewer == null || grid == null || image == null) return;

        double oldZoom = _zoom;
        _zoom += e.Delta.Y > 0 ? ZoomStep : -ZoomStep;
        _zoom = Math.Clamp(_zoom, ZoomMin, ZoomMax);
        if (_zoom == oldZoom) return;

        if (double.IsNaN(_baseContentWidth) || double.IsNaN(_baseContentHeight) || _baseContentWidth <= 0 || _baseContentHeight <= 0)
            UpdateBaseSizeFromBounds();
        if (double.IsNaN(_baseContentWidth) || _baseContentWidth <= 0) return;

        Point pointerInViewport = e.GetPosition(scrollViewer);
        Vector oldOffset = scrollViewer.Offset;

        ApplyZoom();

        double newContentW = _baseContentWidth * _zoom;
        double newContentH = _baseContentHeight * _zoom;
        double extentW = Math.Max(0, newContentW - scrollViewer.Viewport.Width);
        double extentH = Math.Max(0, newContentH - scrollViewer.Viewport.Height);
        double newOffsetX = (oldOffset.X + pointerInViewport.X) * (_zoom / oldZoom) - pointerInViewport.X;
        double newOffsetY = (oldOffset.Y + pointerInViewport.Y) * (_zoom / oldZoom) - pointerInViewport.Y;
        newOffsetX = Math.Clamp(newOffsetX, 0, extentW);
        newOffsetY = Math.Clamp(newOffsetY, 0, extentH);
        scrollViewer.Offset = new Vector(newOffsetX, newOffsetY);

        e.Handled = true;
    }

    private void ImageArea_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var scrollViewer = this.FindControl<ScrollViewer>("ImageScrollViewer");
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && sender is InputElement control && scrollViewer != null)
        {
            e.Pointer.Capture(control);
            _lastPointerPositionForPan = e.GetPosition(scrollViewer);
            _isPanning = true;
            UpdateImageAreaCursor();
        }
    }

    private void ImageArea_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning) return;
        var scrollViewer = this.FindControl<ScrollViewer>("ImageScrollViewer");
        if (scrollViewer == null) return;
        Point current = e.GetPosition(scrollViewer);
        double dx = current.X - _lastPointerPositionForPan.X;
        double dy = current.Y - _lastPointerPositionForPan.Y;
        _lastPointerPositionForPan = current;
        double extentW = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
        double extentH = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        double newX = Math.Clamp(scrollViewer.Offset.X + dx, 0, extentW);
        double newY = Math.Clamp(scrollViewer.Offset.Y + dy, 0, extentH);
        scrollViewer.Offset = new Vector(newX, newY);
    }

    private void ImageArea_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Captured is not null)
            e.Pointer.Capture(null);
        _isPanning = false;
        UpdateImageAreaCursor();
    }

    private void ImageArea_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isPanning = false;
        UpdateImageAreaCursor();
    }

    private void UpdateImageAreaCursor()
    {
        var grid = this.FindControl<Grid>("ImageContainerGrid");
        if (grid == null) return;
        grid.Cursor = _isPanning ? new Cursor(StandardCursorType.SizeAll) : (_zoom > 1.0 ? new Cursor(StandardCursorType.Hand) : null);
    }

    private void UpdateBaseSizeFromBounds()
    {
        if (_zoom != 1.0) return;
        var grid = this.FindControl<Grid>("ImageContainerGrid");
        if (grid == null) return;
        double w = grid.Bounds.Width;
        double h = grid.Bounds.Height;
        if (w > 0 && h > 0)
        {
            _baseContentWidth = w;
            _baseContentHeight = h;
        }
    }

    private void ApplyZoom()
    {
        var scrollViewer = this.FindControl<ScrollViewer>("ImageScrollViewer");
        var grid = this.FindControl<Grid>("ImageContainerGrid");
        var image = this.FindControl<Image>("ImageControl");
        if (scrollViewer == null || grid == null || image == null) return;

        if (_zoom == 1.0)
        {
            grid.Width = double.NaN;
            grid.Height = double.NaN;
            (double fitW, double fitH) = GetFitMaxSize(scrollViewer);
            image.MaxWidth = fitW;
            image.MaxHeight = fitH;
            UpdateImageAreaCursor();
            return;
        }

        if (double.IsNaN(_baseContentWidth) || _baseContentWidth <= 0) return;
        double w = _baseContentWidth * _zoom;
        double h = _baseContentHeight * _zoom;
        grid.Width = w;
        grid.Height = h;
        image.MaxWidth = w;
        image.MaxHeight = h;
        UpdateImageAreaCursor();
    }

    private (double w, double h) GetFitMaxSize(ScrollViewer scrollViewer)
    {
        var viewport = scrollViewer.Viewport;
        double vw = Math.Max(0, viewport.Width - ImageMargin);
        double vh = Math.Max(0, viewport.Height - ImageMargin);
        if (_isMaximized)
            return (vw, vh);
        return (1100, 700);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleMaximize();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _isMaximized)
        {
            ExitMaximize();
            e.Handled = true;
        }
    }

    private void ToggleMaximize()
    {
        if (_isMaximized)
        {
            ExitMaximize();
        }
        else
        {
            EnterMaximize();
        }
    }

    private void EnterMaximize()
    {
        _previousPosition = Position;
        _previousSize = new Size(Width, Height);

        WindowState = WindowState.Maximized;
        _isMaximized = true;

        Resized += OnWindowResized;
        // 首次最大化时在布局完成后按当前视口更新图片尺寸
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateImageMaxSizeForViewport, Avalonia.Threading.DispatcherPriority.Loaded);

        if (this.FindControl<Button>("MaximizeButton") is { } button)
        {
            button.Content = "⤢";
            ToolTip.SetTip(button, "还原 (F11 或双击)");
        }
    }

    private void OnWindowResized(object? sender, WindowResizedEventArgs e)
    {
        UpdateImageMaxSizeForViewport();
        if (_zoom == 1.0)
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateBaseSizeFromBounds, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void UpdateImageMaxSizeForViewport()
    {
        if (!_isMaximized || _zoom != 1.0) return;
        var scrollViewer = this.FindControl<ScrollViewer>("ImageScrollViewer");
        var image = this.FindControl<Image>("ImageControl");
        if (scrollViewer == null || image == null) return;
        var viewport = scrollViewer.Viewport;
        var w = Math.Max(0, viewport.Width - ImageMargin);
        var h = Math.Max(0, viewport.Height - ImageMargin);
        image.MaxWidth = w;
        image.MaxHeight = h;
    }

    private void ExitMaximize()
    {
        Resized -= OnWindowResized;

        if (_zoom == 1.0 && this.FindControl<Image>("ImageControl") is { } image)
        {
            image.MaxWidth = 1100;
            image.MaxHeight = 700;
        }

        WindowState = WindowState.Normal;
        Position = _previousPosition;
        Width = _previousSize.Width;
        Height = _previousSize.Height;

        if (this.FindControl<Button>("MaximizeButton") is { } button)
        {
            button.Content = "⛶";
            ToolTip.SetTip(button, "最大化 (F11 或双击)");
        }

        _isMaximized = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        Loaded -= OnWindowLoaded;
        KeyDown -= OnKeyDown;
        Resized -= OnWindowResized;
        _closeSubscription?.Dispose();

        // 释放 ViewModel（如果实现了 IDisposable）
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }
}
