using System;
using Avalonia.Controls;
using MaterialClient.ViewModels;

namespace MaterialClient.Views;

public partial class AttendedWeighingDetailView : Window
{
    public AttendedWeighingDetailView(AttendedWeighingDetailViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // 订阅事件
        viewModel.SaveCompleted += OnSaveCompleted;
        viewModel.AbolishCompleted += OnAbolishCompleted;
        viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnSaveCompleted(object? sender, EventArgs e)
    {
        // TODO: 可以显示保存成功消息
        Close();
    }

    private void OnAbolishCompleted(object? sender, EventArgs e)
    {
        // TODO: 可以显示废单成功消息
        Close();
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        // 取消订阅事件
        if (DataContext is AttendedWeighingDetailViewModel viewModel)
        {
            viewModel.SaveCompleted -= OnSaveCompleted;
            viewModel.AbolishCompleted -= OnAbolishCompleted;
            viewModel.CloseRequested -= OnCloseRequested;
        }
        base.OnClosed(e);
    }
}
