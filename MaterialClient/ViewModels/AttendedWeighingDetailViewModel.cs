using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using MaterialClient.Views;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Avalonia;

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
    private readonly ILogger<AttendedWeighingDetailViewModel>? _logger;

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

    [Reactive] private Material? _selectedMaterial;

    [Reactive] private int? _selectedMaterialId;

    [Reactive] private ObservableCollection<MaterialUnitDto> _materialUnits = new();

    [Reactive] private MaterialUnitDto? _selectedMaterialUnit;

    [Reactive] private int? _selectedMaterialUnitId;

    [Reactive] private string? _remark;

    [Reactive] private DateTime? _joinTime;

    [Reactive] private DateTime? _outTime;

    [Reactive] private string? _operator;

    [Reactive] private bool _isMatchButtonVisible;

    [Reactive] private string? _plateNumberError;

    [Reactive] private ObservableCollection<MaterialItemRow> _materialItems = new();

    [Reactive] private bool _isLoading;

    #endregion

    public AttendedWeighingDetailViewModel(
        WeighingListItemDto listItem,
        IServiceProvider serviceProvider)
    {
        var totalSw = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();
        
        _listItem = listItem;
        _serviceProvider = serviceProvider;
        _logger = _serviceProvider.GetService<ILogger<AttendedWeighingDetailViewModel>>();
        
        sw.Restart();
        _weighingRecordRepository = _serviceProvider.GetRequiredService<IRepository<WeighingRecord, long>>();
        LogPerf("GetRequiredService<WeighingRecord>", sw);
        
        sw.Restart();
        _materialRepository = _serviceProvider.GetRequiredService<IRepository<Material, int>>();
        LogPerf("GetRequiredService<Material>", sw);
        
        sw.Restart();
        _providerRepository = _serviceProvider.GetRequiredService<IRepository<Provider, int>>();
        LogPerf("GetRequiredService<Provider>", sw);
        
        sw.Restart();
        _materialUnitRepository = _serviceProvider.GetRequiredService<IRepository<MaterialUnit, int>>();
        LogPerf("GetRequiredService<MaterialUnit>", sw);

        sw.Restart();
        InitializeBasicData();
        LogPerf("InitializeBasicData", sw);

        sw.Restart();
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

        // 使用 SelectMany 替代 async void Subscribe
        this.WhenAnyValue(x => x.SelectedMaterial)
            .Where(_ => _isInitialized) // 只在初始化完成后响应
            .SelectMany(async material =>
            {
                if (material != null)
                {
                    SelectedMaterialId = material.Id;
                    await LoadMaterialUnitsAsync(material.Id);
                }
                else
                {
                    SelectedMaterialId = null;
                    MaterialUnits.Clear();
                    SelectedMaterialUnit = null;
                }
                return System.Reactive.Unit.Default;
            })
            .Subscribe();

        this.WhenAnyValue(x => x.SelectedMaterialUnit)
            .Subscribe(unit =>
            {
                if (unit != null)
                {
                    SelectedMaterialUnitId = unit.Id;
                }
            });
        LogPerf("WhenAnyValue subscriptions", sw);
        
        LogPerf("Constructor TOTAL", totalSw);
    }
    
    private void LogPerf(string label, Stopwatch sw, int? count = null)
    {
        if (count.HasValue)
        {
            _logger?.LogInformation("[DetailVM] {Label}: {ElapsedMs}ms (count: {Count})", 
                label, sw.ElapsedMilliseconds, count.Value);
        }
        else
        {
            _logger?.LogInformation("[DetailVM] {Label}: {ElapsedMs}ms", label, sw.ElapsedMilliseconds);
        }
    }

    private bool _isInitialized;

    /// <summary>
    /// 延迟加载数据 - 在 View 加载完成后调用
    /// </summary>
    public async Task LoadDataAsync()
    {
        if (_isInitialized) return;
        
        var totalSw = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();

        IsLoading = true;
        try
        {
            sw.Restart();
            await LoadDropdownDataAsync();
            LogPerf("LoadDropdownDataAsync", sw);
            
            sw.Restart();
            await LoadWeighingRecordDetailsAsync();
            LogPerf("LoadWeighingRecordDetailsAsync", sw);
        }
        finally
        {
            IsLoading = false;
            _isInitialized = true;
            LogPerf("LoadDataAsync TOTAL", totalSw);
        }
    }

    #region 初始化

    /// <summary>
    /// 初始化基础数据（同步，不涉及数据库查询）
    /// </summary>
    private void InitializeBasicData()
    {
        WeighingRecordId = _listItem.Id;
        AllWeight = _listItem.Weight ?? 0;
        TruckWeight = 0;
        GoodsWeight = AllWeight - TruckWeight;
        PlateNumber = _listItem.PlateNumber;
        SelectedProviderId = _listItem.ProviderId;
        SelectedMaterialId = _listItem.MaterialId;
        SelectedMaterialUnitId = _listItem.MaterialUnitId;
        Remark = string.Empty;
        JoinTime = _listItem.JoinTime;
        OutTime = _listItem.OutTime;
        Operator = _listItem.Operator;
        IsMatchButtonVisible = true;

        MaterialItems.Clear();
        MaterialItems.Add(new MaterialItemRow
        {
            LoadMaterialUnitsFunc = LoadMaterialUnitsForRowAsync,
            WaybillQuantity = _listItem.WaybillQuantity,
            WaybillWeight = null,
            ActualQuantity = null,
            ActualWeight = GoodsWeight,
            Difference = null,
            DeviationRate = null,
            DeviationResult = "-"
        });
    }

    private async Task LoadDropdownDataAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            sw.Restart();
            await Task.WhenAll(
                LoadProvidersAsync(),
                LoadMaterialsAsync()
            );
            LogPerf("Task.WhenAll(Providers, Materials)", sw);

            sw.Restart();
            if (SelectedProviderId.HasValue)
            {
                SelectedProvider = Providers.FirstOrDefault(p => p.Id == SelectedProviderId.Value);
            }
            LogPerf("Set SelectedProvider", sw);

            Material? selectedMaterial = null;

            if (SelectedMaterialId.HasValue)
            {
                sw.Restart();
                selectedMaterial = Materials.FirstOrDefault(m => m.Id == SelectedMaterialId.Value);
                SelectedMaterial = selectedMaterial;
                LogPerf("Set SelectedMaterial", sw);

                if (selectedMaterial != null)
                {
                    sw.Restart();
                    await LoadMaterialUnitsAsync(selectedMaterial.Id);
                    LogPerf("LoadMaterialUnitsAsync", sw);
                    
                    if (SelectedMaterialUnitId.HasValue)
                    {
                        SelectedMaterialUnit = MaterialUnits.FirstOrDefault(u => u.Id == SelectedMaterialUnitId.Value);
                    }
                }
            }

            sw.Restart();
            if (MaterialItems.Count > 0)
            {
                var firstRow = MaterialItems[0];
                firstRow.InitializeSelection(Materials, selectedMaterial, MaterialUnits, SelectedMaterialUnitId);
            }
            LogPerf("MaterialItemRow.InitializeSelection", sw);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LoadDropdownDataAsync Error");
        }
    }

    private async Task LoadWeighingRecordDetailsAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var weighingRecord = await _weighingRecordRepository.GetAsync(_listItem.Id);
            LogPerf("DB: GetWeighingRecord", sw);
            IsMatchButtonVisible = weighingRecord.MatchedId == null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LoadWeighingRecordDetailsAsync Error");
        }
    }

    private async Task LoadProvidersAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            sw.Restart();
            var providers = await _providerRepository.GetListAsync();
            LogPerf("DB: GetProviderList ({Count} items)", sw, providers.Count);
            
            sw.Restart();
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
            LogPerf("Build Providers ObservableCollection", sw);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LoadProvidersAsync Error");
        }
    }

    private async Task LoadMaterialsAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            sw.Restart();
            var materials = await _materialRepository.GetListAsync();
            LogPerf("DB: GetMaterialList ({Count} items)", sw, materials.Count);
            
            sw.Restart();
            Materials.Clear();
            foreach (var material in materials.OrderBy(m => m.Name))
            {
                Materials.Add(material);
            }
            LogPerf("Build Materials ObservableCollection", sw);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LoadMaterialsAsync Error");
        }
    }

    private async Task LoadMaterialUnitsAsync(int materialId)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            sw.Restart();
            var units = await _materialUnitRepository.GetListAsync(
                predicate: u => u.MaterialId == materialId
            );
            LogPerf("DB: GetMaterialUnitList ({Count} items)", sw, units.Count);
            
            sw.Restart();
            MaterialUnits.Clear();
            foreach (var unit in units.OrderBy(u => u.UnitName))
            {
                MaterialUnits.Add(new MaterialUnitDto
                {
                    Id = unit.Id,
                    MaterialId = unit.MaterialId,
                    UnitName = unit.UnitName,
                    Rate = unit.Rate ?? 0m,
                    RateName = unit.RateName,
                    ProviderId = unit.ProviderId
                });
            }
            LogPerf("Build MaterialUnits ObservableCollection", sw);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LoadMaterialUnitsAsync Error");
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
        catch
        {
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
                null
            ));

            SaveCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存失败: {ex.Message}");
        }
    }

    [ReactiveCommand]
    private async Task MatchAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(PlateNumber))
            {
                PlateNumberError = "请先在上方填写车牌号后再进行匹配";
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
            System.Diagnostics.Debug.WriteLine($"匹配失败: {ex.Message}");
        }
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
            System.Diagnostics.Debug.WriteLine($"废单失败: {ex.Message}");
        }
    }

    [ReactiveCommand]
    private async Task CompleteAsync()
    {
        await Task.CompletedTask;
    }

    [ReactiveCommand]
    private async Task AddGoodsAsync()
    {
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

    #endregion
}

/// <summary>
/// 材料项行数据（用于 DataGrid）
/// </summary>
public partial class MaterialItemRow : ReactiveObject
{
    public Func<int, Task<ObservableCollection<MaterialUnitDto>>>? LoadMaterialUnitsFunc { get; set; }

    /// <summary>
    /// 可选材料列表（从父 ViewModel 传入，避免跨控件绑定）
    /// </summary>
    [Reactive] private ObservableCollection<Material> _availableMaterials = new();

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

    private bool _isInitialized;

    public MaterialItemRow()
    {
        var sw = Stopwatch.StartNew();
        
        // 使用 SelectMany 替代 async void Subscribe，并添加初始化检查
        this.WhenAnyValue(x => x.SelectedMaterial)
            .Where(_ => _isInitialized) // 只在初始化完成后响应
            .SelectMany(async value =>
            {
                if (value != null && LoadMaterialUnitsFunc != null)
                {
                    await LoadMaterialUnitsInternalAsync(value.Id);
                }
                else
                {
                    MaterialUnits.Clear();
                    SelectedMaterialUnit = null;
                }
                return System.Reactive.Unit.Default;
            })
            .Subscribe();

        this.WhenAnyValue(x => x.SelectedMaterialUnit)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(RateDisplay)));

        this.WhenAnyValue(x => x.WaybillQuantity)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(WaybillQuantityDisplay)));

        this.WhenAnyValue(x => x.WaybillWeight)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(WaybillWeightDisplay)));

        this.WhenAnyValue(x => x.ActualQuantity)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ActualQuantityDisplay)));

        this.WhenAnyValue(x => x.ActualWeight)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ActualWeightDisplay)));

        this.WhenAnyValue(x => x.Difference)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(DifferenceDisplay)));

        this.WhenAnyValue(x => x.DeviationRate)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(DeviationRateDisplay)));
        
        // MaterialItemRow 无法直接使用 ILogger，保留 Debug 输出
        Debug.WriteLine("[MaterialItemRow] Constructor: {0}ms", sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// 标记初始化完成，开始响应属性变更
    /// </summary>
    public void MarkInitialized()
    {
        _isInitialized = true;
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
            catch
            {
                // 如果加载失败，保持空列表
            }
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

    public void InitializeSelection(
        ObservableCollection<Material> availableMaterials,
        Material? material,
        ObservableCollection<MaterialUnitDto> units,
        int? selectedUnitId)
    {
        // 设置可选材料列表
        AvailableMaterials = availableMaterials;

        SelectedMaterial = material;
        SetMaterialUnits(units);

        if (selectedUnitId.HasValue)
        {
            SelectedMaterialUnit = MaterialUnits.FirstOrDefault(u => u.Id == selectedUnitId.Value);
        }

        // 初始化完成后，标记可以响应属性变更
        MarkInitialized();
    }
}