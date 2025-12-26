using System;
using Avalonia.Controls;
using Avalonia.Input;
using MaterialClient.ViewModels;

namespace MaterialClient.Views;

public partial class ImageViewerWindow : Window
{
    private readonly IDisposable? _closeSubscription;

    public ImageViewerWindow(ImageViewerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // 订阅关闭命令
        if (viewModel.CloseCommand != null) _closeSubscription = viewModel.CloseCommand.Subscribe(_ => Close());
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _closeSubscription?.Dispose();

        // 释放 ViewModel（如果实现了 IDisposable）
        if (DataContext is IDisposable disposable) disposable.Dispose();

        base.OnClosed(e);
    }
}