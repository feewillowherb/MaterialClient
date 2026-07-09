using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.ViewModels;
using ReactiveUI;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Views;

public partial class ManualMatchWindow : Window
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly ManualMatchWindowViewModel? _viewModel;
    private readonly ILocalEventBus? _localEventBus;
    private long? _savedWaybillId;
    private readonly CompositeDisposable _disposables = new();

    /// <summary>
    ///     无参构造函数（用于设计器）
    /// </summary>
    public ManualMatchWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     带参数构造函数
    /// </summary>
    /// <param name="currentRecord">当前称重记录</param>
    /// <param name="serviceProvider">服务提供者</param>
    public ManualMatchWindow(WeighingRecord currentRecord, IServiceProvider serviceProvider) : this()
    {
        _serviceProvider = serviceProvider;
        _localEventBus = serviceProvider.GetService(typeof(ILocalEventBus)) as ILocalEventBus;
        _viewModel = new ManualMatchWindowViewModel(currentRecord, serviceProvider);
        DataContext = _viewModel;

        if (_localEventBus != null)
        {
            _localEventBus
                .Subscribe<ManualMatchSaveCompletedEventData>(eventData =>
                {
                    Dispatcher.UIThread.Post(() => { _savedWaybillId = eventData.WaybillId; });
                    return Task.CompletedTask;
                })
                .DisposeWith(_disposables);
        }
    }

    /// <summary>
    ///     选中的匹配记录
    /// </summary>
    public WeighingRecord? SelectedMatchRecord => _viewModel?.SelectedCandidateRecord?.Record;

    /// <summary>
    ///     选中的收发料类型
    /// </summary>
    public DeliveryType SelectedDeliveryType =>
        _viewModel?.SelectedDeliveryType ?? DeliveryType.Receiving;

    /// <summary>
    ///     保存的运单ID
    /// </summary>
    public long? SavedWaybillId => _savedWaybillId;

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private async void OnConfirmButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedCandidateRecord == null || _serviceProvider == null) return;

        // 打开 ManualMatchEditWindow 进行匹配确认编辑
        var editWindow = new ManualMatchEditWindow(
            _viewModel.CurrentRecord,
            _viewModel.SelectedCandidateRecord.Record,
            _viewModel.SelectedDeliveryType,
            _serviceProvider);

        var result = await editWindow.ShowDialog<bool?>(this);

        // 如果用户确认保存，则关闭当前窗口并返回匹配结果
        if (result == true) Close(_viewModel.SelectedCandidateRecord.Record);
    }

    protected override void OnClosed(EventArgs e)
    {
        _disposables.Dispose();
        base.OnClosed(e);
    }
}