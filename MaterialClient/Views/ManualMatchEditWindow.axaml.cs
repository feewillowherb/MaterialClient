using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Views;

public partial class ManualMatchEditWindow : Window
{
    private readonly WeighingRecord? _currentRecord;
    private readonly WeighingRecord? _matchedRecord;
    private readonly DeliveryType _deliveryType;
    private readonly IServiceProvider? _serviceProvider;

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
        _currentRecord = currentRecord;
        _matchedRecord = matchedRecord;
        _deliveryType = deliveryType;
        _serviceProvider = serviceProvider;

        // TODO: 创建 ViewModel 并设置 DataContext
        // DataContext = new ManualMatchEditWindowViewModel(currentRecord, matchedRecord, deliveryType, serviceProvider);
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnConfirmButtonClick(object? sender, RoutedEventArgs e)
    {
        // 确定按钮点击处理
        // TODO: 实现保存逻辑
        Close();
    }
}
