using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MaterialClient.ViewModels;

/// <summary>
///     称重记录详情窗口 ViewModel
/// </summary>
public partial class AttendedWeighingDetailViewModel : ViewModelBase, ITransientDependency
{
    private WeighingListItemDto _listItem;
    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<MaterialUnit, int> _materialUnitRepository;
    private readonly IRepository<Provider, int> _providerRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;

    public AttendedWeighingDetailViewModel(
        IServiceProvider serviceProvider)
        : base(serviceProvider.GetService<ILogger<AttendedWeighingDetailViewModel>>())
    {
        _serviceProvider = serviceProvider;
        _weighingRecordRepository = _serviceProvider.GetRequiredService<IRepository<WeighingRecord, long>>();
        _materialRepository = _serviceProvider.GetRequiredService<IRepository<Material, int>>();
        _providerRepository = _serviceProvider.GetRequiredService<IRepository<Provider, int>>();
        _materialUnitRepository = _serviceProvider.GetRequiredService<IRepository<MaterialUnit, int>>();


        // Setup property change subscriptions
        this.WhenAnyValue(x => x.AllWeight, x => x.TruckWeight)
            .Subscribe(_ => GoodsWeight = AllWeight - TruckWeight);

        this.WhenAnyValue(x => x.PlateNumber)
            .Subscribe(_ => PlateNumberError = null);

        this.WhenAnyValue(x => x.SelectedProvider)
            .Subscribe(provider =>
            {
                if (provider != null) SelectedProviderId = provider.Id;
            });
    }

    #region 属性

    [Reactive] private long _weighingRecordId;

    [Reactive] private decimal _allWeight;

    [Reactive] private decimal _truckWeight;

    [Reactive] private decimal _goodsWeight;

    [Reactive] private string? _plateNumber;

    [Reactive] private ObservableCollection<ProviderDto> _providers = new();

    [Reactive] private ProviderDto? _selectedProvider;

    [Reactive] private int? _selectedProviderId;

    [Reactive] private ObservableCollection<Material> _materials = new();

    [Reactive] private string? _remark;

    [Reactive] private DateTime? _joinTime;

    [Reactive] private DateTime? _outTime;

    [Reactive] private string? _operator;

    [Reactive] private bool _isMatchButtonVisible;

    [Reactive] private bool _isCompleteButtonVisible;

    [Reactive] private string? _plateNumberError;

    [Reactive] private ObservableCollection<MaterialItemRow> _materialItems = new();

    [Reactive] private bool _isMaterialPopupOpen;

    [Reactive] private MaterialItemRow? _currentMaterialRow;

    #endregion

    #region 初始化

    public void InitializeData(WeighingListItemDto listItem)
    {
        _listItem = listItem;
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
        // 根据 ItemType 判断是否显示匹配按钮：Waybill 类型不显示，WeighingRecord 类型在 LoadWeighingRecordDetailsAsync 中根据 MatchedId 判断
        IsMatchButtonVisible = _listItem.ItemType != WeighingListItemType.Waybill;
        // 仅当为 Waybill 且 OrderType == FirstWeight（即未完成）时显示"完成本次收货"按钮
        IsCompleteButtonVisible = _listItem.ItemType == WeighingListItemType.Waybill && !_listItem.IsCompleted;

        MaterialItems.Clear();

        // 从 _listItem.Materials 创建 MaterialItemRow
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
            // 如果没有 Materials，创建一个空行（兼容旧代码）
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

        // 延迟加载数据，避免阻塞 UI 渲染
        Dispatcher.UIThread.Post(LoadDataSafelyAsync, DispatcherPriority.Background);
    }

    private async void LoadDataSafelyAsync()
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

    private async Task LoadDropdownDataAsync()
    {
        try
        {
            await Task.WhenAll(
                LoadProvidersAsync(),
                LoadMaterialsAsync()
            );

            if (SelectedProviderId.HasValue)
                SelectedProvider = Providers.FirstOrDefault(p => p.Id == SelectedProviderId.Value);

            // 根据 _listItem.Materials 初始化每个 MaterialItemRow
            for (var i = 0; i < MaterialItems.Count && i < _listItem.Materials.Count; i++)
            {
                var materialDto = _listItem.Materials[i];
                var row = MaterialItems[i];

                if (materialDto.MaterialId.HasValue)
                {
                    var selectedMaterial = Materials.FirstOrDefault(m => m.Id == materialDto.MaterialId.Value);
                    if (selectedMaterial != null)
                    {
                        var units = await LoadMaterialUnitsForRowAsync(selectedMaterial.Id);
                        row.SetMaterialUnits(units);

                        if (materialDto.MaterialUnitId.HasValue)
                            row.InitializeSelection(selectedMaterial, units, materialDto.MaterialUnitId);
                        else
                            row.InitializeSelection(selectedMaterial, units, null);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载下拉列表数据失败");
            // 如果加载失败，保持当前状态
        }
    }


    private async Task LoadProvidersAsync()
    {
        try
        {
            var providers = await _providerRepository.GetListAsync();
            Providers.Clear();
            foreach (var provider in providers.OrderBy(p => p.ProviderName))
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
            // 如果加载失败，保持空列表
        }
    }

    private async Task LoadMaterialsAsync()
    {
        try
        {
            var materials = await _materialRepository.GetListAsync();
            Materials.Clear();
            foreach (var material in materials.OrderBy(m => m.Name)) Materials.Add(material);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载材料列表失败");
            // 如果加载失败，保持空列表
        }
    }

    private async Task<ObservableCollection<MaterialUnitDto>> LoadMaterialUnitsForRowAsync(int materialId)
    {
        var result = new ObservableCollection<MaterialUnitDto>();
        try
        {
            var units = await _materialUnitRepository.GetListAsync(u => u.MaterialId == materialId
            );
            foreach (var unit in units.OrderBy(u => u.UnitName))
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
            // 如果加载失败，返回空列表
        }

        return result;
    }

    #endregion

    #region 命令

    [ReactiveCommand]
    private async Task SaveAsync()
    {
        try
        {
            // 验证车牌号格式
            if (!PlateNumberValidator.IsValidChinesePlateNumber(PlateNumber))
            {
                await ShowMessageBoxAsync("车牌号不符合规范请修改");
                return;
            }


            var firstRow = MaterialItems.FirstOrDefault();
            var materialId = firstRow?.SelectedMaterial?.Id;
            var materialUnitId = firstRow?.SelectedMaterialUnit?.Id;
            var providerId = SelectedProvider?.Id;
            var waybillQuantity = firstRow?.WaybillQuantity;

            var weighingMatchingService = _serviceProvider.GetRequiredService<IWeighingMatchingService>();
            await weighingMatchingService.UpdateListItemAsync(new UpdateListItemInput(
                _listItem.Id,
                _listItem.ItemType,
                PlateNumber,
                providerId,
                materialId,
                materialUnitId,
                waybillQuantity,
                null,
                Remark
            ));

            var parentViewModel = _serviceProvider.GetRequiredService<AttendedWeighingViewModel>();

            // 检查是否有临时保存的BillPhoto文件，如果有则创建附件
            if (parentViewModel != null && !string.IsNullOrEmpty(parentViewModel.CapturedBillPhotoPath))
            {
                var billPhotoPath = parentViewModel.CapturedBillPhotoPath;

                // 检查文件是否存在
                if (File.Exists(billPhotoPath))
                {
                    var attachmentService = _serviceProvider.GetRequiredService<IAttachmentService>();
                    await attachmentService.CreateOrReplaceBillPhotoAsync(_listItem, billPhotoPath);

                    // 清空临时文件路径
                    parentViewModel.ClearCapturedBillPhotoPath();
                }
            }

            // 发送保存完成消息，通知 UI 选择保存的项
            var message = new SaveCompletedMessage(_listItem.Id, _listItem.ItemType);
            MessageBus.Current.SendMessage(message);

            SaveCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "保存失败");
        }
    }


    [ReactiveCommand]
    private async Task MatchAsync()
    {
        try
        {
            // 验证车牌号格式
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

            // 如果 matchedRecord 不为 null，说明 ManualMatchWindow 已经处理了匹配和保存
            // 不需要再次打开 ManualMatchEditWindow，因为它已经在 ManualMatchWindow 中打开过了
            if (matchedRecord != null)
            {
                MatchCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "匹配失败");
        }
    }

    private async Task ShowMessageBoxAsync(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var parentWin = GetParentWindow();

            // 使用 MessageBoxManager.GetMessageBoxStandard
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

            // 原来的 NotificationManager 方式（已注释）
            // if (parentWin is AttendedWeighingWindow attendedWindow
            //     && attendedWindow.NotificationManager != null)
            //     attendedWindow.NotificationManager.Show(
            //         new Notification("提示", message));
        });
    }

    private Window? GetParentWindow()
    {
        if (Application.Current?.ApplicationLifetime is
            IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;

        return null;
    }

    [ReactiveCommand]
    private async Task AbolishAsync()
    {
        try
        {
            await _weighingRecordRepository.DeleteAsync(_listItem.Id);
            AbolishCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "废单失败");
        }
    }

    [ReactiveCommand]
    private async Task CompleteAsync()
    {
        try
        {
            var firstRow = MaterialItems.FirstOrDefault();
            var materialId = firstRow?.SelectedMaterial?.Id;
            var materialUnitId = firstRow?.SelectedMaterialUnit?.Id;
            var providerId = SelectedProvider?.Id;
            var waybillQuantity = firstRow?.WaybillQuantity;
            var weighingMatchingService = _serviceProvider.GetRequiredService<IWeighingMatchingService>();
            await weighingMatchingService.UpdateListItemAsync(new UpdateListItemInput(
                _listItem.Id,
                _listItem.ItemType,
                PlateNumber,
                providerId,
                materialId,
                materialUnitId,
                waybillQuantity,
                null,
                Remark
            ));

            var parentViewModel = _serviceProvider.GetRequiredService<AttendedWeighingViewModel>();
            
            // 检查是否有临时保存的BillPhoto文件，如果有则创建附件
            if (parentViewModel != null && !string.IsNullOrEmpty(parentViewModel.CapturedBillPhotoPath))
            {
                var billPhotoPath = parentViewModel.CapturedBillPhotoPath;

                // 检查文件是否存在
                if (File.Exists(billPhotoPath))
                {
                    var attachmentService = _serviceProvider.GetRequiredService<IAttachmentService>();
                    await attachmentService.CreateOrReplaceBillPhotoAsync(_listItem, billPhotoPath);

                    // 清空临时文件路径
                    parentViewModel.ClearCapturedBillPhotoPath();
                }
            }

            await weighingMatchingService.CompleteOrderAsync(_listItem.Id);
            CompleteCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "完成本次收货失败");
        }
    }

    [ReactiveCommand]
    private async Task AddMaterialAsync()
    {
        try
        {
            var newRow = new MaterialItemRow
            {
                LoadMaterialUnitsFunc = LoadMaterialUnitsForRowAsync,
                IsWaybill = _listItem.ItemType == WeighingListItemType.Waybill,
                WaybillQuantity = null,
                WaybillWeight = null,
                ActualQuantity = null,
                ActualWeight = 0,
                Difference = null,
                DeviationRate = null,
                DeviationResult = "-"
            };

            MaterialItems.Add(newRow);
            Logger?.LogInformation("已添加新的材料行");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "添加材料行失败");
        }

        await Task.CompletedTask;
    }

    [ReactiveCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [ReactiveCommand]
    private Task OpenMaterialSelectionAsync(MaterialItemRow? row)
    {
        if (row == null) return Task.CompletedTask;

        CurrentMaterialRow = row;
        IsMaterialPopupOpen = true;
        return Task.CompletedTask;
    }

    [ReactiveCommand]
    private Task SelectMaterialAsync(Material? material)
    {
        if (material == null || CurrentMaterialRow == null) return Task.CompletedTask;

        CurrentMaterialRow.SelectedMaterial = material;
        IsMaterialPopupOpen = false;
        CurrentMaterialRow = null;
        return Task.CompletedTask;
    }

    #endregion


    #region 事件

    public event EventHandler? SaveCompleted;
    public event EventHandler? AbolishCompleted;
    public event EventHandler? CloseRequested;
    public event EventHandler? MatchCompleted;
    public event EventHandler? CompleteCompleted;

    #endregion
}

/// <summary>
///     材料项行数据（用于 DataGrid）
/// </summary>
public partial class MaterialItemRow : ReactiveObject
{
    [Reactive] private decimal? _actualQuantity;

    [Reactive] private decimal? _actualWeight;

    [Reactive] private decimal? _deviationRate;

    [Reactive] private string _deviationResult = "-";

    [Reactive] private decimal? _difference;

    [Reactive] private ObservableCollection<MaterialUnitDto> _materialUnits = new();

    [Reactive] private Material? _selectedMaterial;

    [Reactive] private MaterialUnitDto? _selectedMaterialUnit;

    [Reactive] private decimal? _waybillQuantity;

    [Reactive] private decimal? _waybillWeight;

    public MaterialItemRow()
    {
        // 延迟订阅，避免在初始化时触发大量计算
        this.WhenAnyValue(x => x.SelectedMaterial)
            .Subscribe(value =>
            {
                if (value != null && LoadMaterialUnitsFunc != null)
                {
                    // 使用 fire-and-forget 模式，但确保异常被捕获
                    _ = LoadMaterialUnitsSafelyAsync(value.Id);
                }
                else
                {
                    MaterialUnits.Clear();
                    SelectedMaterialUnit = null;
                }

                // 当 Material 变化时触发计算（如果是 Waybill）
                if (IsWaybill) CalculateMaterialWeight();
            });

        this.WhenAnyValue(x => x.SelectedMaterialUnit)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(RateDisplay));
                // 当 MaterialUnit 变化时触发计算（如果是 Waybill）
                if (IsWaybill) CalculateMaterialWeight();
            });

        // 只订阅显示相关的属性变化，避免不必要的 RaisePropertyChanged
        this.WhenAnyValue(x => x.WaybillQuantity)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(WaybillQuantityDisplay));
                // 当 WaybillQuantity 变化时触发计算（如果是 Waybill）
                if (IsWaybill) CalculateMaterialWeight();
            });

        this.WhenAnyValue(x => x.WaybillWeight)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(WaybillWeightDisplay)));

        this.WhenAnyValue(x => x.ActualQuantity)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ActualQuantityDisplay)));

        this.WhenAnyValue(x => x.ActualWeight)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(ActualWeightDisplay));
                // 当 ActualWeight 变化时触发计算（如果是 Waybill）
                if (IsWaybill) CalculateMaterialWeight();
            });

        this.WhenAnyValue(x => x.Difference)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(DifferenceDisplay)));

        this.WhenAnyValue(x => x.DeviationRate)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(DeviationRateDisplay)));
    }

    public Func<int, Task<ObservableCollection<MaterialUnitDto>>>? LoadMaterialUnitsFunc { get; set; }

    /// <summary>
    ///     是否为 Waybill 类型（启用实时计算）
    /// </summary>
    public bool IsWaybill { get; set; }

    public string WaybillQuantityDisplay => WaybillQuantity?.ToString("F2") ?? "";
    public string WaybillWeightDisplay => WaybillWeight?.ToString("F2") ?? "";
    public string ActualQuantityDisplay => ActualQuantity?.ToString("F2") ?? "";
    public string ActualWeightDisplay => ActualWeight?.ToString("F2") ?? "";
    public string DifferenceDisplay => Difference?.ToString("F2") ?? "-";
    public string DeviationRateDisplay => DeviationRate.HasValue ? $"{DeviationRate.Value:F2}%" : "-";
    public string RateDisplay => SelectedMaterialUnit?.Rate.ToString("F2") ?? "";

    /// <summary>
    ///     计算物料重量（使用 MaterialCalculation 统一计算逻辑）
    /// </summary>
    private void CalculateMaterialWeight()
    {
        var calc = new MaterialCalculation(
            WaybillQuantity,
            ActualWeight,
            SelectedMaterialUnit?.Rate,
            SelectedMaterial?.LowerLimit,
            SelectedMaterial?.UpperLimit);

        ApplyCalculation(calc);
    }

    private async Task LoadMaterialUnitsInternalAsync(int materialId)
    {
        if (LoadMaterialUnitsFunc != null)
            try
            {
                var units = await LoadMaterialUnitsFunc(materialId);
                SelectedMaterialUnit = null;
                MaterialUnits.Clear();
                foreach (var unit in units) MaterialUnits.Add(unit);
            }
            catch (Exception)
            {
                // 如果加载失败，保持空列表
                MaterialUnits.Clear();
                SelectedMaterialUnit = null;
            }
    }

    private async Task LoadMaterialUnitsSafelyAsync(int materialId)
    {
        try
        {
            await LoadMaterialUnitsInternalAsync(materialId);
        }
        catch (Exception)
        {
            // 确保异常不会导致应用崩溃
            MaterialUnits.Clear();
            SelectedMaterialUnit = null;
        }
    }

    public void SetMaterialUnits(ObservableCollection<MaterialUnitDto> units)
    {
        MaterialUnits.Clear();
        foreach (var unit in units) MaterialUnits.Add(unit);
    }

    public void InitializeSelection(Material? material, ObservableCollection<MaterialUnitDto> units,
        int? selectedUnitId)
    {
        var originalFunc = LoadMaterialUnitsFunc;
        LoadMaterialUnitsFunc = null;

        SelectedMaterial = material;
        SetMaterialUnits(units);

        if (selectedUnitId.HasValue)
            SelectedMaterialUnit = MaterialUnits.FirstOrDefault(u => u.Id == selectedUnitId.Value);

        LoadMaterialUnitsFunc = originalFunc;
    }

    /// <summary>
    ///     应用物料计算结果
    /// </summary>
    public void ApplyCalculation(MaterialCalculation calc)
    {
        WaybillWeight = calc.PlanWeight;
        ActualQuantity = calc.ActualQuantity;
        Difference = calc.Difference;
        DeviationRate = calc.DeviationRate;
        DeviationResult = calc.OffsetResultDisplay;
    }
}