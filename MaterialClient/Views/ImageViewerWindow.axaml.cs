using System;
using Avalonia.Controls;
using MaterialClient.ViewModels;
using ReactiveUI;

namespace MaterialClient.Views;

public partial class ImageViewerWindow : Window
{
    private IDisposable? _closeSubscription;

    public ImageViewerWindow(ImageViewerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // 订阅关闭命令
        if (viewModel.CloseCommand != null)
        {
            _closeSubscription = viewModel.CloseCommand.Subscribe(_ => Close());
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _closeSubscription?.Dispose();
        base.OnClosed(e);
    }
}

