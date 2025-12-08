using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using Volo.Abp.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient.ViewModels;

/// <summary>
/// 称重记录详情窗口 ViewModel
/// </summary>
public partial class AttendedWeighingDetailViewModel : ViewModelBase
{
    private readonly WeighingRecord _weighingRecord;
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

    #endregion

    public AttendedWeighingDetailViewModel(
        WeighingRecord weighingRecord,
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IRepository<Material, int> materialRepository,
        IRepository<Provider, int> providerRepository,
        IRepository<MaterialUnit, int> materialUnitRepository,
        IServiceProvider serviceProvider)
    {
        _weighingRecord = weighingRecord;
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
        WeighingRecordId = _weighingRecord.Id;
        AllWeight = _weighingRecord.Weight;
        TruckWeight = 0; // TODO: 从数据获取皮重
        GoodsWeight = AllWeight - TruckWeight; // 计算净重
        PlateNumber = _weighingRecord.PlateNumber;
        SelectedProviderId = _weighingRecord.ProviderId;
        SelectedMaterialId = _weighingRecord.MaterialId;
        SelectedMaterialUnitId = _weighingRecord.MaterialUnitId;
        // SelectedProvider, SelectedMaterial, SelectedMaterialUnit 将在加载下拉列表后设置
        WaybillQuantity = _weighingRecord.WaybillQuantity?.ToString("F2");
        Remark = string.Empty; // TODO: 从实体获取备注字段
        JoinTime = _weighingRecord.CreationTime;
        OutTime = null; // TODO: 从数据获取出场时间
        Operator = string.Empty; // TODO: 从数据获取操作员
        IsMatchButtonVisible = _weighingRecord.MatchedId == null;
    }

    private async Task LoadDropdownDataAsync()
    {
        try
        {
            await Task.WhenAll(
                LoadProvidersAsync(),
                LoadMaterialsAsync()
            );

            // 设置已选中的项
            if (SelectedProviderId.HasValue)
            {
                SelectedProvider = Providers.FirstOrDefault(p => p.Id == SelectedProviderId.Value);
            }

            if (SelectedMaterialId.HasValue)
            {
                SelectedMaterial = Materials.FirstOrDefault(m => m.Id == SelectedMaterialId.Value);
                // 如果已有选中的材料，加载对应的单位列表
                if (SelectedMaterial != null)
                {
                    await LoadMaterialUnitsAsync(SelectedMaterial.Id);
                    if (SelectedMaterialUnitId.HasValue)
                    {
                        SelectedMaterialUnit = MaterialUnits.FirstOrDefault(u => u.Id == SelectedMaterialUnitId.Value);
                    }
                }
            }
        }
        catch
        {
            // 如果加载失败，保持空列表
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
                    ProviderType = provider.ProviderType,
                    ProviderName = provider.ProviderName,
                    ContactName = provider.ContactName,
                    ContactPhone = provider.ContactPhone
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
                    Rate = unit.Rate,
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
            // 解析运单数量
            decimal? waybillQuantity = null;
            if (!string.IsNullOrWhiteSpace(WaybillQuantity))
            {
                if (decimal.TryParse(WaybillQuantity, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal quantity))
                {
                    waybillQuantity = quantity;
                }
            }

            // 更新称重记录
            _weighingRecord.Update(
                PlateNumber,
                SelectedProviderId,
                SelectedMaterialId,
                waybillQuantity
            );

            // 更新材料单位ID
            _weighingRecord.MaterialUnitId = SelectedMaterialUnitId;

            // 保存到数据库
            await _weighingRecordRepository.UpdateAsync(_weighingRecord);

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
        // TODO: 实现匹配逻辑
        await Task.CompletedTask;
    }

    /// <summary>
    /// 废单命令（软删除）
    /// </summary>
    [RelayCommand]
    private async Task AbolishAsync()
    {
        try
        {
            // 软删除：调用实体的删除方法
            await _weighingRecordRepository.DeleteAsync(_weighingRecord.Id);

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
        if (!decimal.TryParse(WaybillQuantity, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal result))
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

    #endregion
}