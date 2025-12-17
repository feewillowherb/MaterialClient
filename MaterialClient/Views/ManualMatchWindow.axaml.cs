using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialClient.Common.Entities;
using MaterialClient.ViewModels;

namespace MaterialClient.Views;

public partial class ManualMatchWindow : Window
{
    private readonly ManualMatchWindowViewModel? _viewModel;
    private readonly IServiceProvider? _serviceProvider;

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
        _serviceProvider = serviceProvider;
        _viewModel = new ManualMatchWindowViewModel(currentRecord, serviceProvider);
        DataContext = _viewModel;
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private async void OnConfirmButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedCandidateRecord == null || _serviceProvider == null)
        {
            return;
        }

        // 打开 ManualMatchEditWindow 进行匹配确认编辑
        var editWindow = new ManualMatchEditWindow(
            _viewModel.CurrentRecord,
            _viewModel.SelectedCandidateRecord.Record,
            _viewModel.SelectedDeliveryType,
            _serviceProvider);

        var result = await editWindow.ShowDialog<bool?>(this);
        
        // 如果用户确认保存，则关闭当前窗口并返回匹配结果
        if (result == true)
        {
            Close(_viewModel.SelectedCandidateRecord.Record);
        }
    }
}
