using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using MaterialClient.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views;

public partial class ImageViewerWindow : Window, ITransientDependency
{
    private readonly IDisposable? _closeSubscription;
    private bool _isFullscreen;
    private WindowState _previousWindowState;
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

    private void FullscreenButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _isFullscreen)
        {
            ExitFullscreen();
            e.Handled = true;
        }
    }

    private void ToggleFullscreen()
    {
        if (_isFullscreen)
        {
            ExitFullscreen();
        }
        else
        {
            EnterFullscreen();
        }
    }

    private void EnterFullscreen()
    {
        // 保存当前窗口状态
        _previousWindowState = WindowState;
        _previousPosition = Position;
        _previousSize = new Size(Width, Height);

        // 隐藏标题栏
        if (this.FindControl<Border>("TitleBarBorder") is { } titleBar)
        {
            titleBar.IsVisible = false;
        }

        // 移除图片尺寸限制以充分利用全屏空间
        if (this.FindControl<Image>("ImageControl") is { } image)
        {
            image.MaxWidth = double.PositiveInfinity;
            image.MaxHeight = double.PositiveInfinity;
        }

        // 设置全屏
        WindowState = WindowState.Maximized;
        SystemDecorations = SystemDecorations.None;

        // 更新按钮图标
        if (this.FindControl<Button>("FullscreenButton") is { } button)
        {
            button.Content = "⛝";
            ToolTip.SetTip(button, "退出全屏 (F11 或 ESC)");
        }

        _isFullscreen = true;
    }

    private void ExitFullscreen()
    {
        // 显示标题栏
        if (this.FindControl<Border>("TitleBarBorder") is { } titleBar)
        {
            titleBar.IsVisible = true;
        }

        // 恢复图片尺寸限制
        if (this.FindControl<Image>("ImageControl") is { } image)
        {
            image.MaxWidth = 1100;
            image.MaxHeight = 700;
        }

        // 恢复窗口装饰
        SystemDecorations = SystemDecorations.None;

        // 恢复窗口状态
        WindowState = _previousWindowState;
        if (_previousWindowState == WindowState.Normal)
        {
            Position = _previousPosition;
            Width = _previousSize.Width;
            Height = _previousSize.Height;
        }

        // 更新按钮图标
        if (this.FindControl<Button>("FullscreenButton") is { } button)
        {
            button.Content = "⛶";
            ToolTip.SetTip(button, "全屏 (F11)");
        }

        _isFullscreen = false;
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