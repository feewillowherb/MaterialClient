using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.ViewModels;

namespace MaterialClient.Views;

public partial class ManualMatchEditWindow : Window
{
    private readonly ManualMatchEditWindowViewModel? _viewModel;
    private bool _isSaving;

    /// <summary>
    ///     无参构造函数（用于设计器）
    /// </summary>
    public ManualMatchEditWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     带参数构造函数
    /// </summary>
    /// <param name="currentRecord">当前称重记录</param>
    /// <param name="matchedRecord">匹配的称重记录</param>
    /// <param name="deliveryType">收发料类型</param>
    /// <param name="serviceProvider">服务提供者</param>
    public ManualMatchEditWindow(
        WeighingRecord currentRecord,
        WeighingRecord matchedRecord,
        DeliveryType deliveryType,
        IServiceProvider serviceProvider) : this()
    {
        _viewModel = new ManualMatchEditWindowViewModel(currentRecord, matchedRecord, deliveryType, serviceProvider);
        DataContext = _viewModel;
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close((bool?)false);
    }

    private async void OnConfirmButtonClick(object? sender, RoutedEventArgs e)
    {
        // 防止重复点击
        if (_isSaving) return;

        if (_viewModel == null)
        {
            Close((bool?)false);
            return;
        }

        // 禁用按钮防止重复点击
        _isSaving = true;
        if (sender is Button button) button.IsEnabled = false;

        try
        {
            // 执行保存操作
            var result = await _viewModel.SaveAsync();

            // 如果保存成功，关闭窗口并返回 true
            if (result)
            {
                // 确保在 UI 线程上关闭窗口
                await Dispatcher.UIThread.InvokeAsync(() => Close((bool?)true), DispatcherPriority.Normal);
            }
            else
            {
                // 如果保存失败，重新启用按钮
                _isSaving = false;
                if (sender is Button btn) btn.IsEnabled = true;
            }
        }
        catch
        {
            // 如果出现异常，重新启用按钮
            _isSaving = false;
            if (sender is Button btn) btn.IsEnabled = true;

            throw;
        }
    }
}