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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
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

    /// <summary>
    /// 称重记录ID
    /// </summary>
    [ObservableProperty] private long _weighingRecordId;

    /// <summary>
    /// 毛重
    /// </summary>
    [ObservableProperty] private decimal _allWeight;

    /// <summary>
    /// 皮重
    /// </summary>
    [ObservableProperty] private decimal _truckWeight;

    /// <summary>
    /// 净重
    /// </summary>
    [ObservableProperty] private decimal _goodsWeight;

    /// <summary>
    /// 车牌号
    /// </summary>
    [ObservableProperty] private string? _plateNumber;

    /// <summary>
    /// 供应商列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<ProviderDto> _providers = new();

    /// <summary>
    /// 选中的供应商
    /// </summary>
    [ObservableProperty] private ProviderDto? _selectedProvider;

    /// <summary>
    /// 选中的供应商ID
    /// </summary>
    [ObservableProperty] private int? _selectedProviderId;

    /// <summary>
    /// 材料列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<Material> _materials = new();

    /// <summary>
    /// 选中的材料
    /// </summary>
    [ObservableProperty] private Material? _selectedMaterial;

    /// <summary>
    /// 选中的材料ID
    /// </summary>
    [ObservableProperty] private int? _selectedMaterialId;

    /// <summary>
    /// 材料单位列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<MaterialUnitDto> _materialUnits = new();

    /// <summary>
    /// 选中的材料单位
    /// </summary>
    [ObservableProperty] private MaterialUnitDto? _selectedMaterialUnit;

    /// <summary>
    /// 选中的材料单位ID
    /// </summary>
    [ObservableProperty] private int? _selectedMaterialUnitId;

    /// <summary>
    /// 运单数量
    /// </summary>
    [ObservableProperty] private string? _waybillQuantity;

    /// <summary>
    /// 备注
    /// </summary>
    [ObservableProperty] private string? _remark;

    /// <summary>
    /// 进场时间
    /// </summary>
    [ObservableProperty] private DateTime? _joinTime;

    /// <summary>
    /// 出场时间
    /// </summary>
    [ObservableProperty] private DateTime? _outTime;

    /// <summary>
    /// 收货员工
    /// </summary>
    [ObservableProperty] private string? _operator;

    /// <summary>
    /// 是否显示匹配按钮（MatchedId为null时显示）
    /// </summary>
    [ObservableProperty] private bool _isMatchButtonVisible;

    /// <summary>
    /// 运单数量验证错误信息
    /// </summary>
    [ObservableProperty] private string? _waybillQuantityError;

    /// <summary>
    /// 车牌号错误提示信息
    /// </summary>
    [ObservableProperty] private string? _plateNumberError;

    /// <summary>
    /// 材料项列表（用于 DataGrid 绑定）
    /// </summary>
    [ObservableProperty] private ObservableCollection<MaterialItemRow> _materialItems = new();

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

        // 初始化数据
        InitializeData();

        // 加载下拉列表数据
        _ = LoadDropdownDataAsync();

        // 监听供应商选择变化，更新ID
        this.WhenAnyValue(x => x.SelectedProvider)
            .Subscribe(provider =>
            {
                if (provider != null)
                {
                    SelectedProviderId = provider.Id;
                }
            });

        // 监听材料选择变化，更新ID并动态加载单位列表
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

        // 监听材料单位选择变化，更新ID
        this.WhenAnyValue(x => x.SelectedMaterialUnit)
            .Subscribe(unit =>
            {
                if (unit != null)
                {
                    SelectedMaterialUnitId = unit.Id;
                }
            });
    }

    #region 初始化

    private void InitializeData()
    {
        WeighingRecordId = _listItem.Id;
        AllWeight = _listItem.Weight ?? 0;
        TruckWeight = 0; // TODO: 从数据获取皮重
        GoodsWeight = AllWeight - TruckWeight; // 计算净重
        PlateNumber = _listItem.PlateNumber;
        SelectedProviderId = _listItem.ProviderId;
        SelectedMaterialId = _listItem.MaterialId;
        SelectedMaterialUnitId = _listItem.MaterialUnitId;
        // SelectedProvider, SelectedMaterial, SelectedMaterialUnit 将在加载下拉列表后设置
        WaybillQuantity = _listItem.WaybillQuantity?.ToString("F2");
        Remark = string.Empty; // TODO: 从实体获取备注字段
        JoinTime = _listItem.JoinTime;
        OutTime = _listItem.OutTime;
        Operator = string.Empty; // TODO: 从数据获取操作员
        IsMatchButtonVisible = true; // 默认显示，需要从 WeighingRecord 获取 MatchedId

        // 初始化材料项列表（添加一行默认数据）
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
        
        // 异步加载完整的 WeighingRecord 信息（用于 MatchedId 等字段）
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

            // 设置已选中的供应商
            if (SelectedProviderId.HasValue)
            {
                SelectedProvider = Providers.FirstOrDefault(p => p.Id == SelectedProviderId.Value);
            }

            // 设置已选中的材料和单位
            Material? selectedMaterial = null;

            if (SelectedMaterialId.HasValue)
            {
                selectedMaterial = Materials.FirstOrDefault(m => m.Id == SelectedMaterialId.Value);
                SelectedMaterial = selectedMaterial;
                
                // 如果已有选中的材料，加载对应的单位列表
                if (selectedMaterial != null)
                {
                    await LoadMaterialUnitsAsync(selectedMaterial.Id);
                    if (SelectedMaterialUnitId.HasValue)
                    {
                        SelectedMaterialUnit = MaterialUnits.FirstOrDefault(u => u.Id == SelectedMaterialUnitId.Value);
                    }
                }
            }

            // 设置 DataGrid 第一行的材料和单位（使用初始化方法，避免触发自动加载）
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
            
            // 更新需要从 WeighingRecord 获取的字段（主要是 MatchedId）
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

    /// <summary>
    /// 为 DataGrid 行加载材料单位列表
    /// </summary>
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

    /// <summary>
    /// 保存命令
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        // 验证运单数量
        if (!ValidateWaybillQuantity())
        {
            return;
        }

        try
        {
            // 从数据库获取最新的 WeighingRecord
            var weighingRecord = await _weighingRecordRepository.GetAsync(_listItem.Id);
            
            // 解析运单数量
            decimal? waybillQuantity = null;
            if (!string.IsNullOrWhiteSpace(WaybillQuantity))
            {
                if (decimal.TryParse(WaybillQuantity, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                        out decimal quantity))
                {
                    waybillQuantity = quantity;
                }
            }

            // 从 DataGrid 行获取材料和单位ID
            var firstRow = MaterialItems.FirstOrDefault();
            int? materialId = firstRow?.SelectedMaterial?.Id;
            int? materialUnitId = firstRow?.SelectedMaterialUnit?.Id;

            // 直接从 SelectedProvider 获取供应商ID
            int? providerId = SelectedProvider?.Id;

            // 更新称重记录
            weighingRecord.Update(
                PlateNumber,
                providerId,
                materialId,
                materialUnitId,
                waybillQuantity
            );

            // 保存到数据库
            await _weighingRecordRepository.UpdateAsync(weighingRecord);

            // 触发保存成功事件
            SaveCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // TODO: 显示错误消息
            System.Diagnostics.Debug.WriteLine($"保存失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 匹配命令
    /// </summary>
    [RelayCommand]
    private async Task MatchAsync()
    {
        try
        {
            // 检查车牌号是否为空
            if (string.IsNullOrWhiteSpace(PlateNumber))
            {
                // 提示用户需要先填写车牌号
                PlateNumberError = "请先在上方填写车牌号后再进行匹配";
                return;
            }

            // 从数据库获取最新的 WeighingRecord
            var weighingRecord = await _weighingRecordRepository.GetAsync(_listItem.Id);

            // 打开手动匹配窗口
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

            // 如果用户选择了匹配记录
            if (matchedRecord != null)
            {
                // 打开 ManualMatchEditWindow
                var editWindow = new ManualMatchEditWindow(weighingRecord, matchedRecord, matchWindow.SelectedDeliveryType, _serviceProvider);
                await editWindow.ShowDialog(parentWin);
                
                // 匹配完成后刷新
                MatchCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"匹配失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取父窗口
    /// </summary>
    private Avalonia.Controls.Window? GetParentWindow()
    {
        // 尝试从应用程序获取主窗口
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    /// <summary>
    /// 废单命令（软删除）
    /// </summary>
    [RelayCommand]
    private async Task AbolishAsync()
    {
        try
        {
            // 软删除
            await _weighingRecordRepository.DeleteAsync(_listItem.Id);

            // 触发删除成功事件
            AbolishCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // TODO: 显示错误消息
            System.Diagnostics.Debug.WriteLine($"废单失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 完成本次收货命令
    /// </summary>
    [RelayCommand]
    private async Task CompleteAsync()
    {
        // TODO: 实现完成本次收货逻辑
        await Task.CompletedTask;
    }

    /// <summary>
    /// 新增材料命令
    /// </summary>
    [RelayCommand]
    private async Task AddGoodsAsync()
    {
        // TODO: 实现新增材料逻辑（可能需要打开新窗口）
        await Task.CompletedTask;
    }

    /// <summary>
    /// 关闭命令
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region 验证

    /// <summary>
    /// 验证运单数量
    /// </summary>
    private bool ValidateWaybillQuantity()
    {
        WaybillQuantityError = null;

        if (string.IsNullOrWhiteSpace(WaybillQuantity))
        {
            return true; // 允许为空
        }

        // 验证是否为有效的十进制数
        if (!decimal.TryParse(WaybillQuantity, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                out decimal result))
        {
            WaybillQuantityError = "请输入有效的数字";
            return false;
        }

        // 验证是否大于0
        if (result <= 0)
        {
            WaybillQuantityError = "运单数量必须大于0";
            return false;
        }

        // 验证小数位数（最多两位）
        var parts = WaybillQuantity.Split('.');
        if (parts.Length > 1 && parts[1].Length > 2)
        {
            WaybillQuantityError = "运单数量最多保留两位小数";
            return false;
        }

        return true;
    }

    #endregion

    #region 属性变更处理

    partial void OnWaybillQuantityChanged(string? value)
    {
        // 清除之前的错误信息
        WaybillQuantityError = null;
    }

    partial void OnPlateNumberChanged(string? value)
    {
        // 清除车牌号错误提示
        PlateNumberError = null;
    }

    partial void OnTruckWeightChanged(decimal value)
    {
        // 重新计算净重
        GoodsWeight = AllWeight - value;
    }

    partial void OnAllWeightChanged(decimal value)
    {
        // 重新计算净重
        GoodsWeight = value - TruckWeight;
    }

    #endregion

    #region 事件

    /// <summary>
    /// 保存完成事件
    /// </summary>
    public event EventHandler? SaveCompleted;

    /// <summary>
    /// 废单完成事件
    /// </summary>
    public event EventHandler? AbolishCompleted;

    /// <summary>
    /// 关闭请求事件
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// 匹配完成事件
    /// </summary>
    public event EventHandler? MatchCompleted;

    #endregion
}

/// <summary>
/// 材料项行数据（用于 DataGrid）
/// </summary>
public partial class MaterialItemRow : ObservableObject
{
    /// <summary>
    /// 用于加载材料单位的委托
    /// </summary>
    public Func<int, Task<ObservableCollection<MaterialUnitDto>>>? LoadMaterialUnitsFunc { get; set; }

    /// <summary>
    /// 选中的材料
    /// </summary>
    [ObservableProperty] private Material? _selectedMaterial;

    /// <summary>
    /// 选中的材料单位
    /// </summary>
    [ObservableProperty] private MaterialUnitDto? _selectedMaterialUnit;

    /// <summary>
    /// 材料单位列表（行级别）
    /// </summary>
    [ObservableProperty] private ObservableCollection<MaterialUnitDto> _materialUnits = new();

    /// <summary>
    /// 运单数量
    /// </summary>
    [ObservableProperty] private decimal? _waybillQuantity;

    /// <summary>
    /// 运单重量
    /// </summary>
    [ObservableProperty] private decimal? _waybillWeight;

    /// <summary>
    /// 实际数量
    /// </summary>
    [ObservableProperty] private decimal? _actualQuantity;

    /// <summary>
    /// 实际重量
    /// </summary>
    [ObservableProperty] private decimal? _actualWeight;

    /// <summary>
    /// 正负差
    /// </summary>
    [ObservableProperty] private decimal? _difference;

    /// <summary>
    /// 偏差率
    /// </summary>
    [ObservableProperty] private decimal? _deviationRate;

    /// <summary>
    /// 偏差结果
    /// </summary>
    [ObservableProperty] private string _deviationResult = "-";

    // 显示属性（用于格式化显示，null 时显示空字符串或 "-"）
    public string WaybillQuantityDisplay => WaybillQuantity?.ToString("F2") ?? "";
    public string WaybillWeightDisplay => WaybillWeight?.ToString("F2") ?? "";
    public string ActualQuantityDisplay => ActualQuantity?.ToString("F2") ?? "";
    public string ActualWeightDisplay => ActualWeight?.ToString("F2") ?? "";
    public string DifferenceDisplay => Difference?.ToString("F2") ?? "-";
    public string DeviationRateDisplay => DeviationRate.HasValue ? $"{DeviationRate.Value:F2}%" : "-";
    public string RateDisplay => SelectedMaterialUnit?.Rate.ToString("F2") ?? "";

    // 属性变更通知
    partial void OnWaybillQuantityChanged(decimal? value) => OnPropertyChanged(nameof(WaybillQuantityDisplay));
    partial void OnWaybillWeightChanged(decimal? value) => OnPropertyChanged(nameof(WaybillWeightDisplay));
    partial void OnActualQuantityChanged(decimal? value) => OnPropertyChanged(nameof(ActualQuantityDisplay));
    partial void OnActualWeightChanged(decimal? value) => OnPropertyChanged(nameof(ActualWeightDisplay));
    partial void OnDifferenceChanged(decimal? value) => OnPropertyChanged(nameof(DifferenceDisplay));
    partial void OnDeviationRateChanged(decimal? value) => OnPropertyChanged(nameof(DeviationRateDisplay));
    partial void OnSelectedMaterialUnitChanged(MaterialUnitDto? value) => OnPropertyChanged(nameof(RateDisplay));

    /// <summary>
    /// 当选中的材料变化时，加载对应的单位列表
    /// </summary>
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

    /// <summary>
    /// 加载材料单位列表
    /// </summary>
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

    /// <summary>
    /// 设置单位列表（外部调用，用于初始化）
    /// </summary>
    public void SetMaterialUnits(ObservableCollection<MaterialUnitDto> units)
    {
        MaterialUnits.Clear();
        foreach (var unit in units)
        {
            MaterialUnits.Add(unit);
        }
    }

    /// <summary>
    /// 初始化行数据（不触发自动加载）
    /// </summary>
    public void InitializeSelection(Material? material, ObservableCollection<MaterialUnitDto> units, int? selectedUnitId)
    {
        // 暂时禁用自动加载
        var originalFunc = LoadMaterialUnitsFunc;
        LoadMaterialUnitsFunc = null;

        // 设置材料（不会触发自动加载）
        SelectedMaterial = material;
        
        // 设置单位列表
        SetMaterialUnits(units);
        
        // 从新列表中查找并设置选中的单位（确保对象引用一致）
        if (selectedUnitId.HasValue)
        {
            SelectedMaterialUnit = MaterialUnits.FirstOrDefault(u => u.Id == selectedUnitId.Value);
        }

        // 恢复自动加载功能
        LoadMaterialUnitsFunc = originalFunc;
    }
}