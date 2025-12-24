using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using MaterialClient.Common.Providers;
using MaterialClient.Views;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace MaterialClient.ViewModels;

/// <summary>
/// 称重记录详情窗口 ViewModel
/// </summary>
public partial class AttendedWeighingDetailViewModel : ViewModelBase
{
    private readonly WeighingListItemDto _listItem;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<Provider, int> _providerRepository;
    private readonly IRepository<MaterialUnit, int> _materialUnitRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly AttendedWeighingViewModel? _parentViewModel;

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

    #endregion

    public AttendedWeighingDetailViewModel(
        WeighingListItemDto listItem,
        IServiceProvider serviceProvider,
        AttendedWeighingViewModel? parentViewModel = null)
        : base(serviceProvider.GetService<ILogger<AttendedWeighingDetailViewModel>>())
    {
        _listItem = listItem;
        _serviceProvider = serviceProvider;
        _parentViewModel = parentViewModel;
        _weighingRecordRepository = _serviceProvider.GetRequiredService<IRepository<WeighingRecord, long>>();
        _materialRepository = _serviceProvider.GetRequiredService<IRepository<Material, int>>();
        _providerRepository = _serviceProvider.GetRequiredService<IRepository<Provider, int>>();
        _materialUnitRepository = _serviceProvider.GetRequiredService<IRepository<MaterialUnit, int>>();

        InitializeData();

        // Setup property change subscriptions
        this.WhenAnyValue(x => x.AllWeight, x => x.TruckWeight)
            .Subscribe(_ => GoodsWeight = AllWeight - TruckWeight);

        this.WhenAnyValue(x => x.PlateNumber)
            .Subscribe(_ => PlateNumberError = null);

        this.WhenAnyValue(x => x.SelectedProvider)
            .Subscribe(provider =>
            {
                if (provider != null)
                {
                    SelectedProviderId = provider.Id;
                }
            });
    }

    #region 初始化

    private void InitializeData()
    {
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
        IsMatchButtonVisible = true;
        // 仅当为 Waybill 且 OrderType == FirstWeight（即未完成）时显示"完成本次收货"按钮
        IsCompleteButtonVisible = _listItem.ItemType == WeighingListItemType.Waybill && !_listItem.IsCompleted;

        MaterialItems.Clear();
        
        // 从 _listItem.Materials 创建 MaterialItemRow
        if (_listItem.Materials.Count > 0)
        {
            foreach (var materialDto in _listItem.Materials)
            {
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
            }
        }
        else
        {
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
        }

        // 延迟加载数据，避免阻塞 UI 渲染
        Dispatcher.UIThread.Post(LoadDataSafelyAsync, DispatcherPriority.Background);
    }

    private async void LoadDataSafelyAsync()
    {
        try
        {
            await LoadWeighingRecordDetailsAsync();
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
            {
                SelectedProvider = Providers.FirstOrDefault(p => p.Id == SelectedProviderId.Value);
            }

            // 根据 _listItem.Materials 初始化每个 MaterialItemRow
            for (int i = 0; i < MaterialItems.Count && i < _listItem.Materials.Count; i++)
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
                        {
                            row.InitializeSelection(selectedMaterial, units, materialDto.MaterialUnitId);
                        }
                        else
                        {
                            row.InitializeSelection(selectedMaterial, units, null);
                        }
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

    private async Task LoadWeighingRecordDetailsAsync()
    {
        try
        {
            var weighingRecord = await _weighingRecordRepository.GetAsync(_listItem.Id);
            IsMatchButtonVisible = weighingRecord.MatchedId == null;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载称重记录详情失败，RecordId={RecordId}", _listItem.Id);
            // 如果加载失败，保持默认值
        }
    }

    private async Task LoadProvidersAsync()
    {
        try
        {
            var providers = await _providerRepository.GetListAsync();
            Providers.Clear();
            foreach (var provider in providers.OrderBy(p => p.ProviderName))
            {
                Providers.Add(new ProviderDto
                {
                    Id = provider.Id,
                    ProviderType = provider.ProviderType ?? 0,
                    ProviderName = provider.ProviderName,
                    ContactName = provider.ContectName,
                    ContactPhone = provider.ContectPhone
                });
            }
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
            foreach (var material in materials.OrderBy(m => m.Name))
            {
                Materials.Add(material);
            }
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
            var units = await _materialUnitRepository.GetListAsync(
                predicate: u => u.MaterialId == materialId
            );
            foreach (var unit in units.OrderBy(u => u.UnitName))
            {
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
            var firstRow = MaterialItems.FirstOrDefault();
            int? materialId = firstRow?.SelectedMaterial?.Id;
            int? materialUnitId = firstRow?.SelectedMaterialUnit?.Id;
            int? providerId = SelectedProvider?.Id;
            decimal? waybillQuantity = firstRow?.WaybillQuantity;

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

            // 检查是否有临时保存的BillPhoto文件，如果有则创建附件
            if (_parentViewModel != null && !string.IsNullOrEmpty(_parentViewModel.CapturedBillPhotoPath))
            {
                var billPhotoPath = _parentViewModel.CapturedBillPhotoPath;
                
                // 检查文件是否存在
                if (File.Exists(billPhotoPath))
                {
                    var attachmentService = _serviceProvider.GetRequiredService<IAttachmentService>();
                    await attachmentService.CreateOrReplaceBillPhotoAsync(_listItem, billPhotoPath);
                    
                    // 清空临时文件路径
                    _parentViewModel.ClearCapturedBillPhotoPath();
                }
            }

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

            if (matchedRecord != null)
            {
                var editWindow = new ManualMatchEditWindow(weighingRecord, matchedRecord,
                    matchWindow.SelectedDeliveryType, _serviceProvider);
                await editWindow.ShowDialog(parentWin);
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
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var parentWin = GetParentWindow();
            if (parentWin is Views.AttendedWeighing.AttendedWeighingWindow attendedWindow 
                && attendedWindow.NotificationManager != null)
            {
                attendedWindow.NotificationManager.Show(
                    new Notification("提示", message, NotificationType.Information));
            }
        });
    }

    private Avalonia.Controls.Window? GetParentWindow()
    {
        if (Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

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
            int? materialId = firstRow?.SelectedMaterial?.Id;
            int? materialUnitId = firstRow?.SelectedMaterialUnit?.Id;
            int? providerId = SelectedProvider?.Id;
            decimal? waybillQuantity = firstRow?.WaybillQuantity;
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
/// 材料项行数据（用于 DataGrid）
/// </summary>
public partial class MaterialItemRow : ReactiveObject
{
    public Func<int, Task<ObservableCollection<MaterialUnitDto>>>? LoadMaterialUnitsFunc { get; set; }

    /// <summary>
    /// 是否为 Waybill 类型（启用实时计算）
    /// </summary>
    public bool IsWaybill { get; set; }

    [Reactive] private Material? _selectedMaterial;

    [Reactive] private MaterialUnitDto? _selectedMaterialUnit;

    [Reactive] private ObservableCollection<MaterialUnitDto> _materialUnits = new();

    [Reactive] private decimal? _waybillQuantity;

    [Reactive] private decimal? _waybillWeight;

    [Reactive] private decimal? _actualQuantity;

    [Reactive] private decimal? _actualWeight;

    [Reactive] private decimal? _difference;

    [Reactive] private decimal? _deviationRate;

    [Reactive] private string _deviationResult = "-";

    public string WaybillQuantityDisplay => WaybillQuantity?.ToString("F2") ?? "";
    public string WaybillWeightDisplay => WaybillWeight?.ToString("F2") ?? "";
    public string ActualQuantityDisplay => ActualQuantity?.ToString("F2") ?? "";
    public string ActualWeightDisplay => ActualWeight?.ToString("F2") ?? "";
    public string DifferenceDisplay => Difference?.ToString("F2") ?? "-";
    public string DeviationRateDisplay => DeviationRate.HasValue ? $"{DeviationRate.Value:F2}%" : "-";
    public string RateDisplay => SelectedMaterialUnit?.Rate.ToString("F2") ?? "";

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
                if (IsWaybill)
                {
                    CalculateMaterialWeight();
                }
            });

        this.WhenAnyValue(x => x.SelectedMaterialUnit)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(RateDisplay));
                // 当 MaterialUnit 变化时触发计算（如果是 Waybill）
                if (IsWaybill)
                {
                    CalculateMaterialWeight();
                }
            });

        // 只订阅显示相关的属性变化，避免不必要的 RaisePropertyChanged
        this.WhenAnyValue(x => x.WaybillQuantity)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(WaybillQuantityDisplay));
                // 当 WaybillQuantity 变化时触发计算（如果是 Waybill）
                if (IsWaybill)
                {
                    CalculateMaterialWeight();
                }
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
                if (IsWaybill)
                {
                    CalculateMaterialWeight();
                }
            });

        this.WhenAnyValue(x => x.Difference)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(DifferenceDisplay)));

        this.WhenAnyValue(x => x.DeviationRate)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(DeviationRateDisplay)));
    }

    /// <summary>
    /// 计算物料重量（使用 MaterialCalculation 统一计算逻辑）
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
        {
            try
            {
                var units = await LoadMaterialUnitsFunc(materialId);
                SelectedMaterialUnit = null;
                MaterialUnits.Clear();
                foreach (var unit in units)
                {
                    MaterialUnits.Add(unit);
                }
            }
            catch (Exception)
            {
                // 如果加载失败，保持空列表
                MaterialUnits.Clear();
                SelectedMaterialUnit = null;
            }
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
        foreach (var unit in units)
        {
            MaterialUnits.Add(unit);
        }
    }

    public void InitializeSelection(Material? material, ObservableCollection<MaterialUnitDto> units,
        int? selectedUnitId)
    {
        var originalFunc = LoadMaterialUnitsFunc;
        LoadMaterialUnitsFunc = null;

        SelectedMaterial = material;
        SetMaterialUnits(units);

        if (selectedUnitId.HasValue)
        {
            SelectedMaterialUnit = MaterialUnits.FirstOrDefault(u => u.Id == selectedUnitId.Value);
        }

        LoadMaterialUnitsFunc = originalFunc;
    }

    /// <summary>
    /// 应用物料计算结果
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