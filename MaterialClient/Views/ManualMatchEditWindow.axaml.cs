using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.ViewModels;

namespace MaterialClient.Views;

public partial class ManualMatchEditWindow : Window
{
    private readonly ManualMatchEditWindowViewModel? _viewModel;

    /// <summary>
    /// 无参构造函数（用于设计器）
    /// </summary>
    public ManualMatchEditWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 带参数构造函数
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
        Close(false);
    }

    private async void OnConfirmButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
        {
            Close(false);
            return;
        }

        // 执行保存操作
        var result = await _viewModel.SaveAsync();
        
        // 如果保存成功，关闭窗口并返回 true
        if (result)
        {
            Close(true);
        }
    }
}
