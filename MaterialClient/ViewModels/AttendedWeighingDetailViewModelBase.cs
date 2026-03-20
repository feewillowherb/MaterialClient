using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Models;
using MaterialClient.Common.Providers;
using MaterialClient.Common.Services;
using MaterialClient.Views;
using MaterialClient.Views.AttendedWeighing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.Domain.Repositories;

namespace MaterialClient.ViewModels;

/// <summary>
///     称重记录详情窗口 ViewModel 抽象基类
///     包含标准模式和固废模式的共享逻辑
/// </summary>
public abstract partial class AttendedWeighingDetailViewModelBase : ViewModelBase
{
    public sealed record DeliveryTypeOption(DeliveryType Value, string DisplayName);

    protected WeighingListItemDto _listItem = null!;
    protected readonly IServiceProvider _serviceProvider;
    protected readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private string? _capturedBillPhotoPath;

    protected AttendedWeighingDetailViewModelBase(
        IServiceProvider serviceProvider,
        ILogger? logger = null)
        : base(logger)
    {
        _serviceProvider = serviceProvider;
        _weighingRecordRepository = _serviceProvider.GetRequiredService<IRepository<WeighingRecord, long>>();

        // Setup property change subscriptions
        this.WhenAnyValue(x => x.AllWeight, x => x.TruckWeight)
            .Subscribe(_ => GoodsWeight = AllWeight - TruckWeight);

        this.WhenAnyValue(x => x.PlateNumber)
            .Subscribe(_ => PlateNumberError = null);

        this.WhenAnyValue(x => x.SelectedDeliveryType)
            .Subscribe(deliveryType =>
            {
                if (_listItem == null) return;

                _listItem.DeliveryType = deliveryType;
                this.RaisePropertyChanged(nameof(DeliveryTypeDisplayText));
                this.RaisePropertyChanged(nameof(ProviderLabelText));
                this.RaisePropertyChanged(nameof(DeliveryTypeTitleText));
                this.RaisePropertyChanged(nameof(CompleteButtonText));
            });

        // 订阅 WeighingMode 变化，更新 IsSolidWasteMode
        this.WhenAnyValue(x => x.WeighingMode)
            .Subscribe(mode => IsSolidWasteMode = mode == WeighingMode.SolidWaste);
    }

    #region 共享属性

    public sealed record ConfirmTextRequest(string Title, string Message, string InitialValue);

    /// <summary>
    ///     由 View 层注册处理器并弹出确认对话框。
    ///     返回 null 表示取消；返回非空字符串表示确认后的输入。
    /// </summary>
    public Interaction<ConfirmTextRequest, string?> ConfirmTextInteraction { get; } = new();

    [Reactive] private long _weighingRecordId;

    [Reactive] private decimal _allWeight;

    [Reactive] private decimal _truckWeight;

    [Reactive] private decimal _goodsWeight;

    [Reactive] private string? _plateNumber;

    [Reactive] private int? _selectedProviderId;

    [Reactive] private string? _remark;

    [Reactive] private DateTime? _joinTime;

    [Reactive] private DateTime? _outTime;

    [Reactive] private string? _operator;

    [Reactive] private bool _isMatchButtonVisible;

    [Reactive] private bool _isCompleteButtonVisible;

    [Reactive] private string? _plateNumberError;

    [Reactive] private ObservableCollection<MaterialItemRow> _materialItems = new();

    [Reactive] private WeighingMode _weighingMode = WeighingMode.Standard;

    [Reactive] private bool _isSolidWasteMode;

    [Reactive] private DeliveryType _selectedDeliveryType = DeliveryType.Receiving;

    /// <summary>
    ///     供应商标签文本（根据当前记录的收发料类型动态显示）
    /// </summary>
    public string ProviderLabelText
    {
        get
        {
            return _listItem?.DeliveryType == DeliveryType.Receiving
                ? "发货单位"
                : "收货单位";
        }
    }

    public string DeliveryTypeTitleText
    {
        get
        {
            return _listItem?.DeliveryType switch
            {
                DeliveryType.Sending => "发料信息",
                DeliveryType.Receiving => "收料信息",
                _ => "物料信息"
            };
        }
    }

    /// <summary>
    ///     完成按钮文本（根据当前记录的收发料类型动态显示）
    /// </summary>
    public string CompleteButtonText
    {
        get
        {
            var deliveryType = _listItem?.DeliveryType ?? DeliveryType.Receiving;
            return deliveryType == DeliveryType.Sending ? "完成本次发货" : "完成本次收货";
        }
    }

    public bool IsWeighingRecord => _listItem != null && _listItem.ItemType == WeighingListItemType.WeighingRecord;

    public IReadOnlyList<DeliveryTypeOption> DeliveryTypeOptions { get; } =
    [
        new(DeliveryType.Receiving, "收料"),
        new(DeliveryType.Sending, "发料")
    ];

    public string DeliveryTypeDisplayText => (_listItem?.DeliveryType ?? DeliveryType.Receiving) switch
    {
        DeliveryType.Sending => "发料",
        _ => "收料"
    };

    #endregion

    #region 初始化

    public virtual void InitializeData(WeighingListItemDto listItem, string? capturedBillPhotoPath = null)
    {
        _listItem = listItem;
        SelectedDeliveryType = _listItem.DeliveryType ?? DeliveryType.Receiving;
        WeighingRecordId = _listItem.Id;
        AllWeight = _listItem.Weight ?? 0;
        TruckWeight = _listItem.TruckWeight ?? 0;
        GoodsWeight = AllWeight - TruckWeight;
        PlateNumber = _listItem.PlateNumber;
        SelectedProviderId = _listItem.ProviderId;
        Remark = _listItem.Remark ?? string.Empty;
        JoinTime = _listItem.JoinTime;
        OutTime = _listItem.OutTime;
        Operator = _listItem.Operator;

        WeighingMode = _listItem.WeighingMode;

        this.RaisePropertyChanged(nameof(ProviderLabelText));
        this.RaisePropertyChanged(nameof(DeliveryTypeTitleText));
        this.RaisePropertyChanged(nameof(CompleteButtonText));
        this.RaisePropertyChanged(nameof(DeliveryTypeDisplayText));
        this.RaisePropertyChanged(nameof(IsWeighingRecord));

        _capturedBillPhotoPath = capturedBillPhotoPath;

        IsMatchButtonVisible = _listItem.ItemType != WeighingListItemType.Waybill;
        IsCompleteButtonVisible = _listItem.ItemType == WeighingListItemType.Waybill && !_listItem.IsCompleted;

        MaterialItems.Clear();

        if (_listItem.Materials.Count > 0)
            foreach (var materialDto in _listItem.Materials)
                MaterialItems.Add(new MaterialItemRow
                {
                    LoadMaterialUnitsFunc = LoadMaterialUnitsForRowAsync,
                    IsWaybill = _listItem.ItemType == WeighingListItemType.Waybill,
                    WaybillQuantity = materialDto.WaybillQuantity,
                    WaybillWeight = null,
                    ActualQuantity = null,
                    ActualWeight = materialDto.Weight ?? GoodsWeight,
                    Difference = null,
                    DeviationRate = null,
                    DeviationResult = "-"
                });
        else
            MaterialItems.Add(new MaterialItemRow
            {
                LoadMaterialUnitsFunc = LoadMaterialUnitsForRowAsync,
                IsWaybill = _listItem.ItemType == WeighingListItemType.Waybill,
                WaybillQuantity = _listItem.WaybillQuantity,
                WaybillWeight = null,
                ActualQuantity = null,
                ActualWeight = GoodsWeight,
                Difference = null,
                DeviationRate = null,
                DeviationResult = "-"
            });

        Dispatcher.UIThread.Post(LoadDataSafelyAsync, DispatcherPriority.Background);
    }

    protected async void LoadDataSafelyAsync()
    {
        try
        {
            await LoadDropdownDataAsync();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载详情数据失败");
        }
    }

    protected abstract Task LoadDropdownDataAsync();

    protected abstract Task<ObservableCollection<MaterialUnitDto>> LoadMaterialUnitsForRowAsync(int materialId);

    #endregion

    #region 命令

    [ReactiveCommand]
    protected async Task SaveAsync()
    {
        try
        {
            if (!PlateNumberValidator.IsValidChinesePlateNumber(PlateNumber))
            {
                await ShowMessageBoxAsync("车牌号不符合规范请修改");
                return;
            }

            await SaveCoreAsync();

            if (!string.IsNullOrEmpty(_capturedBillPhotoPath))
            {
                var billPhotoPath = _capturedBillPhotoPath;

                if (File.Exists(billPhotoPath))
                {
                    var attachmentService = _serviceProvider.GetRequiredService<IAttachmentService>();
                    await attachmentService.CreateOrReplaceBillPhotoAsync(_listItem, billPhotoPath);
                    _capturedBillPhotoPath = null;
                }
            }

            var message = new SaveCompletedMessage(_listItem.Id, _listItem.ItemType);
            MessageBus.Current.SendMessage(message);

            SaveCompleted?.Invoke(this, new ItemOperationCompletedEventArgs(
                itemId: _listItem.Id,
                itemType: _listItem.ItemType,
                orderType: _listItem.OrderType,
                isCompleted: _listItem.OrderType == OrderTypeEnum.Completed,
                operationType: "Save"));

            Dispatcher.UIThread.Post(() =>
            {
                var parentWin = GetParentWindow();
                if (parentWin is AttendedWeighingWindow attendedWindow
                    && attendedWindow.NotificationManager != null)
                    attendedWindow.NotificationManager.Show(
                        new Notification("提示", "保存成功", NotificationType.Success));
            });
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "保存失败");
        }
    }

    [ReactiveCommand]
    protected async Task MatchAsync()
    {
        try
        {
            if (!PlateNumberValidator.IsValidChinesePlateNumber(PlateNumber))
            {
                await ShowMessageBoxAsync("车牌号不符合规范请修改");
                return;
            }

            var weighingRecord = await _weighingRecordRepository.GetAsync(_listItem.Id);
            var matchWindow = new ManualMatchWindow(weighingRecord, _serviceProvider);

            var parentWin = GetParentWindow();
            WeighingRecord? matchedRecord;

            if (parentWin != null)
            {
                matchedRecord = await matchWindow.ShowDialog<WeighingRecord?>(parentWin);
            }
            else
            {
                matchWindow.Show();
                return;
            }

            if (matchedRecord != null)
            {
                if (matchWindow.SavedWaybillId.HasValue)
                {
                    ManualMatchSaveCompleted?.Invoke(this, new ItemOperationCompletedEventArgs(
                        itemId: matchWindow.SavedWaybillId.Value,
                        itemType: WeighingListItemType.Waybill,
                        orderType: OrderTypeEnum.FirstWeight,
                        isCompleted: false,
                        operationType: "ManualMatch"));
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "匹配失败");
        }
    }

    [ReactiveCommand]
    protected async Task AbolishAsync()
    {
        var result = await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var parentWin = GetParentWindow();
            var messageBox = MessageBoxManager.GetMessageBoxStandard(
                "确认废单",
                "确定要废除此单吗？",
                ButtonEnum.YesNo,
                Icon.Question);

            if (parentWin != null)
                return await messageBox.ShowWindowDialogAsync(parentWin);
            return await messageBox.ShowAsync();
        });

        if (result != ButtonResult.Yes)
            return;

        try
        {
            await _weighingRecordRepository.DeleteAsync(_listItem.Id);

            AbolishCompleted?.Invoke(this, new ItemOperationCompletedEventArgs(
                itemId: _listItem.Id,
                itemType: _listItem.ItemType,
                orderType: _listItem.OrderType,
                isCompleted: false,
                operationType: "Abolish"));
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "废单失败");
        }
    }

    [ReactiveCommand]
    protected async Task CompleteAsync()
    {
        try
        {
            if (!PlateNumberValidator.IsValidChinesePlateNumber(PlateNumber))
            {
                ShowMessageBoxAsyncWithoutBlocking("车牌号不符合规范请修改");
                return;
            }

            await CompleteCoreAsync();

            if (!string.IsNullOrEmpty(_capturedBillPhotoPath))
            {
                var billPhotoPath = _capturedBillPhotoPath;

                if (File.Exists(billPhotoPath))
                {
                    var attachmentService = _serviceProvider.GetRequiredService<IAttachmentService>();
                    await attachmentService.CreateOrReplaceBillPhotoAsync(_listItem, billPhotoPath);
                    _capturedBillPhotoPath = null;
                }
            }

            CompleteCompleted?.Invoke(this, new ItemOperationCompletedEventArgs(
                itemId: _listItem.Id,
                itemType: WeighingListItemType.Waybill,
                orderType: OrderTypeEnum.Completed,
                isCompleted: true,
                operationType: "Complete"));
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "完成本次收货失败");
        }
    }

    [ReactiveCommand]
    protected void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region 抽象方法

    /// <summary>
    ///     保存核心逻辑（由派生类实现）
    /// </summary>
    protected abstract Task SaveCoreAsync();

    /// <summary>
    ///     完成核心逻辑（由派生类实现）
    /// </summary>
    protected abstract Task CompleteCoreAsync();

    #endregion

    #region 辅助方法

    protected async Task ShowMessageBoxAsync(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var parentWin = GetParentWindow();

            var messageBox = MessageBoxManager.GetMessageBoxStandard(
                "提示",
                message,
                ButtonEnum.Ok,
                Icon.None);

            if (parentWin != null)
            {
                await messageBox.ShowWindowDialogAsync(parentWin);
            }
            else
            {
                await messageBox.ShowAsync();
            }
        });
    }

    protected void ShowMessageBoxAsyncWithoutBlocking(string message)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            var parentWin = GetParentWindow();

            var messageBox = MessageBoxManager.GetMessageBoxStandard(
                "提示",
                message,
                ButtonEnum.Ok,
                Icon.None);

            if (parentWin != null)
            {
                await messageBox.ShowWindowDialogAsync(parentWin);
            }
            else
            {
                await messageBox.ShowAsync();
            }
        }, DispatcherPriority.Normal);
    }

    protected Window? GetParentWindow()
    {
        if (Application.Current?.ApplicationLifetime is
            IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;

        return null;
    }

    #endregion

    #region 事件

    public event EventHandler<ItemOperationCompletedEventArgs>? SaveCompleted;
    public event EventHandler<ItemOperationCompletedEventArgs>? AbolishCompleted;
    public event EventHandler? CloseRequested;
    public event EventHandler<ItemOperationCompletedEventArgs>? MatchCompleted;
    public event EventHandler<ItemOperationCompletedEventArgs>? CompleteCompleted;
    public event EventHandler<ItemOperationCompletedEventArgs>? ManualMatchSaveCompleted;

    #endregion
}
