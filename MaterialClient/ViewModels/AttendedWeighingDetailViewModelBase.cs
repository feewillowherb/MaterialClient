using System;
using System.Collections.Generic;
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
///     Abstract base class for attended weighing detail view models,
///     containing shared properties, fields, events, commands, and helper methods.
/// </summary>
public abstract partial class AttendedWeighingDetailViewModelBase : ViewModelBase
{
    public sealed record DeliveryTypeOption(DeliveryType Value, string DisplayName);

    public sealed record ConfirmTextRequest(string Title, string Message, string InitialValue);

    private protected WeighingListItemDto _listItem = null!;
    private protected readonly IServiceProvider _serviceProvider;
    private protected readonly IMaterialService _materialService;
    private protected readonly IProviderService _providerService;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private protected string? _capturedBillPhotoPath;

    /// <summary>
    ///     Interaction for text confirmation dialogs.
    ///     The View layer registers a handler to show a confirmation dialog.
    ///     Returns null if cancelled; returns a non-null string for confirmed input.
    /// </summary>
    public Interaction<ConfirmTextRequest, string?> ConfirmTextInteraction { get; } = new();

    protected AttendedWeighingDetailViewModelBase(
        IServiceProvider serviceProvider,
        ILogger? logger)
        : base(logger)
    {
        _serviceProvider = serviceProvider;
        _weighingRecordRepository = _serviceProvider.GetRequiredService<IRepository<WeighingRecord, long>>();
        _materialService = _serviceProvider.GetRequiredService<IMaterialService>();
        _providerService = _serviceProvider.GetRequiredService<IProviderService>();

        // Subscribe to weight changes -> update GoodsWeight
        this.WhenAnyValue(x => x.AllWeight, x => x.TruckWeight)
            .Subscribe(_ => GoodsWeight = AllWeight - TruckWeight);

        // Subscribe to PlateNumber changes -> clear error
        this.WhenAnyValue(x => x.PlateNumber)
            .Subscribe(_ => PlateNumberError = null);

        // Subscribe to SelectedProvider -> sync SelectedProviderId
        this.WhenAnyValue(x => x.SelectedProvider)
            .Subscribe(provider =>
            {
                if (provider != null) SelectedProviderId = provider.Id;
            });

        // Subscribe to SelectedDeliveryType -> update display text properties
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
    }

    #region Shared Reactive Properties

    [Reactive] private long _weighingRecordId;
    [Reactive] private decimal _allWeight;
    [Reactive] private decimal _truckWeight;
    [Reactive] private decimal _goodsWeight;
    [Reactive] private string? _plateNumber;
    [Reactive] private string? _remark;
    [Reactive] private DateTime? _joinTime;
    [Reactive] private DateTime? _outTime;
    [Reactive] private string? _operator;
    [Reactive] private bool _isMatchButtonVisible;
    [Reactive] private bool _isCompleteButtonVisible;
    [Reactive] private string? _plateNumberError;
    [Reactive] private ObservableCollection<MaterialItemRow> _materialItems = new();
    [Reactive] private DeliveryType _selectedDeliveryType = DeliveryType.Receiving;

    // Shared collections used by both modes for lookup
    [Reactive] private ObservableCollection<ProviderDto> _providers = new();
    [Reactive] private ProviderDto? _selectedProvider;
    [Reactive] private int? _selectedProviderId;
    [Reactive] private ObservableCollection<Material> _materials = new();

    #endregion

    #region Computed Properties

    /// <summary>
    ///     Whether this view model is in SolidWaste mode.
    /// </summary>
    public abstract bool IsSolidWasteMode { get; }

    /// <summary>
    ///     Provider label text (dynamic based on delivery type).
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
    ///     Complete button text (dynamic based on delivery type).
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

    #region Abstract Methods

    /// <summary>
    ///     Save mode-specific data. Called by the SaveAsync template method.
    /// </summary>
    protected abstract Task SaveModeSpecificAsync();

    /// <summary>
    ///     Complete mode-specific data. Called by the CompleteAsync template method.
    /// </summary>
    protected abstract Task CompleteModeSpecificAsync();

    #endregion

    #region Virtual Methods

    /// <summary>
    ///     Load mode-specific dropdown data after shared data has been loaded.
    ///     Override in subclasses to load mode-specific collections.
    /// </summary>
    protected virtual Task LoadModeSpecificDataAsync() => Task.CompletedTask;

    #endregion

    #region Shared Helper Methods

    private protected async Task ShowMessageBoxAsync(string message)
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

    /// <summary>
    ///     Show a message box asynchronously without blocking command execution,
    ///     used to release button locked state when validation fails.
    /// </summary>
    private protected void ShowMessageBoxAsyncWithoutBlocking(string message)
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

    private protected Window? GetParentWindow()
    {
        if (Application.Current?.ApplicationLifetime is
            IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;

        return null;
    }

    #endregion

    #region Shared Data Loading Methods

    private protected async Task LoadProvidersAsync()
    {
        try
        {
            var providers = await _providerService.GetAllProvidersAsync();
            Providers.Clear();
            foreach (var provider in providers)
                Providers.Add(new ProviderDto
                {
                    Id = provider.Id,
                    ProviderType = provider.ProviderType ?? 0,
                    ProviderName = provider.ProviderName,
                    ContactName = provider.ContectName,
                    ContactPhone = provider.ContectPhone
                });
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载供应商列表失败");
        }
    }

    private protected async Task LoadMaterialsAsync()
    {
        try
        {
            var materials = await _materialService.GetAllMaterialsAsync();
            Materials.Clear();
            foreach (var material in materials) Materials.Add(material);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载材料列表失败");
        }
    }

    private protected async Task<ObservableCollection<MaterialUnitDto>> LoadMaterialUnitsForRowAsync(int materialId)
    {
        var result = new ObservableCollection<MaterialUnitDto>();
        try
        {
            var units = await _materialService.GetMaterialUnitsByMaterialIdAsync(materialId);
            foreach (var unit in units)
                result.Add(new MaterialUnitDto
                {
                    Id = unit.Id,
                    MaterialId = unit.MaterialId,
                    UnitName = unit.UnitName,
                    Rate = unit.Rate ?? 0m,
                    RateName = unit.RateName,
                    ProviderId = unit.ProviderId
                });
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载材料单位失败，MaterialId={MaterialId}", materialId);
        }

        return result;
    }

    #endregion

    #region Initialization

    public void InitializeData(WeighingListItemDto listItem, string? capturedBillPhotoPath = null)
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

        // Notify computed property changes
        this.RaisePropertyChanged(nameof(ProviderLabelText));
        this.RaisePropertyChanged(nameof(DeliveryTypeTitleText));
        this.RaisePropertyChanged(nameof(CompleteButtonText));
        this.RaisePropertyChanged(nameof(DeliveryTypeDisplayText));
        this.RaisePropertyChanged(nameof(IsWeighingRecord));

        // Save temporary captured bill photo path
        _capturedBillPhotoPath = capturedBillPhotoPath;

        // Determine button visibility based on ItemType
        IsMatchButtonVisible = _listItem.ItemType != WeighingListItemType.Waybill;
        IsCompleteButtonVisible = _listItem.ItemType == WeighingListItemType.Waybill && !_listItem.IsCompleted;

        MaterialItems.Clear();

        // Create MaterialItemRows from _listItem.Materials
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

        // Defer loading to avoid blocking UI rendering
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await LoadDropdownDataAsync();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "加载详情数据失败");
            }
        }, DispatcherPriority.Background);
    }

    private async Task LoadDropdownDataAsync()
    {
        try
        {
            // Load shared data in parallel
            await Task.WhenAll(
                LoadProvidersAsync(),
                LoadMaterialsAsync()
            );

            // Call mode-specific loading (recommendation, MaterialItemRow init, SolidWaste data, etc.)
            await LoadModeSpecificDataAsync();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载下拉列表数据失败");
        }
    }

    #endregion

    #region Commands

    [ReactiveCommand]
    private async Task SaveAsync()
    {
        try
        {
            // Validate plate number format
            if (!PlateNumberValidator.IsValidChinesePlateNumber(PlateNumber))
            {
                await ShowMessageBoxAsync("车牌号不符合规范请修改");
                return;
            }

            await SaveModeSpecificAsync();

            // Check if there is a temporary BillPhoto file to create attachment
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

            // Send save completed message
            var message = new SaveCompletedMessage(_listItem.Id, _listItem.ItemType);
            MessageBus.Current.SendMessage(message);

            // Trigger save completed event with full operation context
            SaveCompleted?.Invoke(this, new ItemOperationCompletedEventArgs(
                itemId: _listItem.Id,
                itemType: _listItem.ItemType,
                orderType: _listItem.OrderType,
                isCompleted: _listItem.OrderType == OrderTypeEnum.Completed,
                operationType: "Save"));

            // Show success notification
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
    private async Task CompleteAsync()
    {
        try
        {
            // Validate plate number format
            if (!PlateNumberValidator.IsValidChinesePlateNumber(PlateNumber))
            {
                ShowMessageBoxAsyncWithoutBlocking("车牌号不符合规范请修改");
                return;
            }

            await CompleteModeSpecificAsync();

            // Check if there is a temporary BillPhoto file to create attachment
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

            // Trigger complete event with full operation context
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
    private async Task AbolishAsync()
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
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [ReactiveCommand]
    private async Task MatchAsync()
    {
        try
        {
            // Validate plate number format
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

            // If matchedRecord is not null, ManualMatchWindow has already handled matching and saving
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

    #endregion

    #region Events

    public event EventHandler<ItemOperationCompletedEventArgs>? SaveCompleted;
    public event EventHandler<ItemOperationCompletedEventArgs>? AbolishCompleted;
    public event EventHandler? CloseRequested;
    public event EventHandler<ItemOperationCompletedEventArgs>? MatchCompleted;
    public event EventHandler<ItemOperationCompletedEventArgs>? CompleteCompleted;
    public event EventHandler<ItemOperationCompletedEventArgs>? ManualMatchSaveCompleted;

    #endregion
}
