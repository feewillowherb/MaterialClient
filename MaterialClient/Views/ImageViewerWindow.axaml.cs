using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using MaterialClient.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views;

public partial class ImageViewerWindow : Window, ITransientDependency
{
    private const double ImageMargin = 40; // 20 each side
    private readonly IDisposable? _closeSubscription;
    private bool _isMaximized;
    private PixelPoint _previousPosition;
    private Size _previousSize;

    public ImageViewerWindow(ImageViewerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // 订阅关闭命令
        if (viewModel.CloseCommand != null)
        {
            _closeSubscription = viewModel.CloseCommand.Subscribe(_ => Close());
        }

        // 添加键盘快捷键支持
        KeyDown += OnKeyDown;
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
            button.Content = "⛝";
            ToolTip.SetTip(button, "还原 (F11 或双击)");
        }
    }

    private void OnWindowResized(object? sender, WindowResizedEventArgs e)
    {
        UpdateImageMaxSizeForViewport();
    }

    private void UpdateImageMaxSizeForViewport()
    {
        if (!_isMaximized) return;
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

        if (this.FindControl<Image>("ImageControl") is { } image)
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
        KeyDown -= OnKeyDown;
        _closeSubscription?.Dispose();

        // 释放 ViewModel（如果实现了 IDisposable）
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }
}
