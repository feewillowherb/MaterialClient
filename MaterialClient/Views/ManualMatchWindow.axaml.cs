using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialClient.Common.Entities;
using MaterialClient.ViewModels;

namespace MaterialClient.Views;

public partial class ManualMatchWindow : Window
{
    private readonly ManualMatchWindowViewModel? _viewModel;

    /// <summary>
    /// 选中的匹配记录
    /// </summary>
    public WeighingRecord? SelectedMatchRecord => _viewModel?.SelectedCandidateRecord?.Record;

    /// <summary>
    /// 选中的收发料类型
    /// </summary>
    public Common.Entities.Enums.DeliveryType SelectedDeliveryType => 
        _viewModel?.SelectedDeliveryType ?? Common.Entities.Enums.DeliveryType.Receiving;

    /// <summary>
    /// 无参构造函数（用于设计器）
    /// </summary>
    public ManualMatchWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 带参数构造函数
    /// </summary>
    /// <param name="currentRecord">当前称重记录</param>
    /// <param name="serviceProvider">服务提供者</param>
    public ManualMatchWindow(WeighingRecord currentRecord, IServiceProvider serviceProvider) : this()
    {
        _viewModel = new ManualMatchWindowViewModel(currentRecord, serviceProvider);
        DataContext = _viewModel;
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private async void OnConfirmButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedCandidateRecord == null)
        {
            return;
        }

        // 返回选中的记录
        Close(_viewModel.SelectedCandidateRecord.Record);
    }
}
