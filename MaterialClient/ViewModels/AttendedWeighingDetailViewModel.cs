using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using MaterialClient.Views;
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

    #region 属性

    [Reactive]
    private long _weighingRecordId;

    [Reactive]
    private decimal _allWeight;

    [Reactive]
    private decimal _truckWeight;

    [Reactive]
    private decimal _goodsWeight;

    [Reactive]
    private string? _plateNumber;

    [Reactive]
    private ObservableCollection<ProviderDto> _providers = new();

    [Reactive]
    private ProviderDto? _selectedProvider;

    [Reactive]
    private int? _selectedProviderId;

    [Reactive]
    private ObservableCollection<Material> _materials = new();

    [Reactive]
    private Material? _selectedMaterial;

    [Reactive]
    private int? _selectedMaterialId;

    [Reactive]
    private ObservableCollection<MaterialUnitDto> _materialUnits = new();

    [Reactive]
    private MaterialUnitDto? _selectedMaterialUnit;

    [Reactive]
    private int? _selectedMaterialUnitId;

    [Reactive]
    private string? _waybillQuantity;

    [Reactive]
    private string? _remark;

    [Reactive]
    private DateTime? _joinTime;

    [Reactive]
    private DateTime? _outTime;

    [Reactive]
    private string? _operator;

    [Reactive]
    private bool _isMatchButtonVisible;

    [Reactive]
    private string? _waybillQuantityError;

    [Reactive]
    private string? _plateNumberError;

    [Reactive]
    private ObservableCollection<MaterialItemRow> _materialItems = new();

    #endregion

    public AttendedWeighingDetailViewModel(
        WeighingListItemDto listItem,
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IRepository<Material, int> materialRepository,
        IRepository<Provider, int> providerRepository,
        IRepository<MaterialUnit, int> materialUnitRepository,
        IServiceProvider serviceProvider)
    {
        _listItem = listItem;
        _weighingRecordRepository = weighingRecordRepository;
        _materialRepository = materialRepository;
        _providerRepository = providerRepository;
        _materialUnitRepository = materialUnitRepository;
        _serviceProvider = serviceProvider;

        InitializeData();
        _ = LoadDropdownDataAsync();

        this.WhenAnyValue(x => x.SelectedProvider)
            .Subscribe(provider =>
            {
                if (provider != null)
                {
                    SelectedProviderId = provider.Id;
                }
            });

        this.WhenAnyValue(x => x.SelectedMaterial)
            .Subscribe(async material =>
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
            });

        this.WhenAnyValue(x => x.SelectedMaterialUnit)
            .Subscribe(unit =>
            {
                if (unit != null)
                {
                    SelectedMaterialUnitId = unit.Id;
                }
            });
    }

    partial void OnAllWeightChanged(decimal value)
    {
        GoodsWeight = value - TruckWeight;
    }

    partial void OnTruckWeightChanged(decimal value)
    {
        GoodsWeight = AllWeight - value;
    }

    partial void OnWaybillQuantityChanged(string? value)
    {
        WaybillQuantityError = null;
    }

    partial void OnPlateNumberChanged(string? value)
    {
        PlateNumberError = null;
    }

    #region 初始化

    private void InitializeData()
    {
        WeighingRecordId = _listItem.Id;
        AllWeight = _listItem.Weight ?? 0;
        TruckWeight = 0;
        GoodsWeight = AllWeight - TruckWeight;
        PlateNumber = _listItem.PlateNumber;
        SelectedProviderId = _listItem.ProviderId;
        SelectedMaterialId = _listItem.MaterialId;
        SelectedMaterialUnitId = _listItem.MaterialUnitId;
        WaybillQuantity = _listItem.WaybillQuantity?.ToString("F2");
        Remark = string.Empty;
        JoinTime = _listItem.JoinTime;
        OutTime = _listItem.OutTime;
        Operator = string.Empty;
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
        
        _ = LoadWeighingRecordDetailsAsync();
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

            Material? selectedMaterial = null;

            if (SelectedMaterialId.HasValue)
            {
                selectedMaterial = Materials.FirstOrDefault(m => m.Id == SelectedMaterialId.Value);
                SelectedMaterial = selectedMaterial;
                
                if (selectedMaterial != null)
                {
                    await LoadMaterialUnitsAsync(selectedMaterial.Id);
                    if (SelectedMaterialUnitId.HasValue)
                    {
                        SelectedMaterialUnit = MaterialUnits.FirstOrDefault(u => u.Id == SelectedMaterialUnitId.Value);
                    }
                }
            }

            if (MaterialItems.Count > 0)
            {
                var firstRow = MaterialItems[0];
                firstRow.InitializeSelection(selectedMaterial, MaterialUnits, SelectedMaterialUnitId);
            }
        }
        catch
        {
            // 如果加载失败，保持空列表
        }
    }

    private async Task LoadWeighingRecordDetailsAsync()
    {
        try
        {
            var weighingRecord = await _weighingRecordRepository.GetAsync(_listItem.Id);
            IsMatchButtonVisible = weighingRecord.MatchedId == null;
        }
        catch
        {
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
        catch
        {
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
        catch
        {
            // 如果加载失败，保持空列表
        }
    }

    private async Task LoadMaterialUnitsAsync(int materialId)
    {
        try
        {
            var units = await _materialUnitRepository.GetListAsync(
                predicate: u => u.MaterialId == materialId
            );
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
        }
        catch
        {
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
        if (!ValidateWaybillQuantity())
        {
            return;
        }

        try
        {
            var weighingRecord = await _weighingRecordRepository.GetAsync(_listItem.Id);
            
            decimal? waybillQuantity = null;
            if (!string.IsNullOrWhiteSpace(WaybillQuantity))
            {
                if (decimal.TryParse(WaybillQuantity, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                        out decimal quantity))
                {
                    waybillQuantity = quantity;
                }
            }

            var firstRow = MaterialItems.FirstOrDefault();
            int? materialId = firstRow?.SelectedMaterial?.Id;
            int? materialUnitId = firstRow?.SelectedMaterialUnit?.Id;
            int? providerId = SelectedProvider?.Id;

            weighingRecord.Update(
                PlateNumber,
                providerId,
                materialId,
                materialUnitId,
                waybillQuantity
            );

            await _weighingRecordRepository.UpdateAsync(weighingRecord);
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
                var editWindow = new ManualMatchEditWindow(weighingRecord, matchedRecord, matchWindow.SelectedDeliveryType, _serviceProvider);
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
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
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

    #region 验证

    private bool ValidateWaybillQuantity()
    {
        WaybillQuantityError = null;

        if (string.IsNullOrWhiteSpace(WaybillQuantity))
        {
            return true;
        }

        if (!decimal.TryParse(WaybillQuantity, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                out decimal result))
        {
            WaybillQuantityError = "请输入有效的数字";
            return false;
        }

        if (result <= 0)
        {
            WaybillQuantityError = "运单数量必须大于0";
            return false;
        }

        var parts = WaybillQuantity.Split('.');
        if (parts.Length > 1 && parts[1].Length > 2)
        {
            WaybillQuantityError = "运单数量最多保留两位小数";
            return false;
        }

        return true;
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

    [Reactive]
    private Material? _selectedMaterial;

    [Reactive]
    private MaterialUnitDto? _selectedMaterialUnit;

    [Reactive]
    private ObservableCollection<MaterialUnitDto> _materialUnits = new();

    [Reactive]
    private decimal? _waybillQuantity;

    [Reactive]
    private decimal? _waybillWeight;

    [Reactive]
    private decimal? _actualQuantity;

    [Reactive]
    private decimal? _actualWeight;

    [Reactive]
    private decimal? _difference;

    [Reactive]
    private decimal? _deviationRate;

    [Reactive]
    private string _deviationResult = "-";

    public string WaybillQuantityDisplay => WaybillQuantity?.ToString("F2") ?? "";
    public string WaybillWeightDisplay => WaybillWeight?.ToString("F2") ?? "";
    public string ActualQuantityDisplay => ActualQuantity?.ToString("F2") ?? "";
    public string ActualWeightDisplay => ActualWeight?.ToString("F2") ?? "";
    public string DifferenceDisplay => Difference?.ToString("F2") ?? "-";
    public string DeviationRateDisplay => DeviationRate.HasValue ? $"{DeviationRate.Value:F2}%" : "-";
    public string RateDisplay => SelectedMaterialUnit?.Rate.ToString("F2") ?? "";

    partial void OnSelectedMaterialChanged(Material? value)
    {
        if (value != null && LoadMaterialUnitsFunc != null)
        {
            _ = LoadMaterialUnitsInternalAsync(value.Id);
        }
        else
        {
            MaterialUnits.Clear();
            SelectedMaterialUnit = null;
        }
    }

    partial void OnSelectedMaterialUnitChanged(MaterialUnitDto? value)
    {
        this.RaisePropertyChanged(nameof(RateDisplay));
    }

    partial void OnWaybillQuantityChanged(decimal? value)
    {
        this.RaisePropertyChanged(nameof(WaybillQuantityDisplay));
    }

    partial void OnWaybillWeightChanged(decimal? value)
    {
        this.RaisePropertyChanged(nameof(WaybillWeightDisplay));
    }

    partial void OnActualQuantityChanged(decimal? value)
    {
        this.RaisePropertyChanged(nameof(ActualQuantityDisplay));
    }

    partial void OnActualWeightChanged(decimal? value)
    {
        this.RaisePropertyChanged(nameof(ActualWeightDisplay));
    }

    partial void OnDifferenceChanged(decimal? value)
    {
        this.RaisePropertyChanged(nameof(DifferenceDisplay));
    }

    partial void OnDeviationRateChanged(decimal? value)
    {
        this.RaisePropertyChanged(nameof(DeviationRateDisplay));
    }

    private async Task LoadMaterialUnitsInternalAsync(int materialId)
    {
        if (LoadMaterialUnitsFunc != null)
        {
            try
            {
                var units = await LoadMaterialUnitsFunc(materialId);
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

    public void InitializeSelection(Material? material, ObservableCollection<MaterialUnitDto> units, int? selectedUnitId)
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
}
