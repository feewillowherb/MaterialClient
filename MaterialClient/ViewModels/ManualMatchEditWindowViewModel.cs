using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Volo.Abp.Domain.Repositories;

namespace MaterialClient.ViewModels;

/// <summary>
/// 手动匹配编辑窗口 ViewModel
/// </summary>
public partial class ManualMatchEditWindowViewModel : ViewModelBase
{
    private readonly WeighingRecord _currentRecord;
    private readonly WeighingRecord _matchedRecord;
    private readonly DeliveryType _deliveryType;
    private readonly IServiceProvider _serviceProvider;
    private readonly IRepository<Provider, int>? _providerRepository;
    private readonly IRepository<Material, int>? _materialRepository;
    private readonly IRepository<MaterialUnit, int>? _materialUnitRepository;
    private readonly IRepository<WeighingRecordAttachment, int>? _attachmentRepository;
    private readonly IRepository<WeighingRecord, long>? _weighingRecordRepository;

    #region 属性

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
    /// 称毛时间
    /// </summary>
    [ObservableProperty] private DateTime? _grossWeightTime;

    /// <summary>
    /// 称皮时间
    /// </summary>
    [ObservableProperty] private DateTime? _tareWeightTime;

    /// <summary>
    /// 毛重（吨）
    /// </summary>
    [ObservableProperty] private decimal _grossWeight;

    /// <summary>
    /// 皮重（吨）
    /// </summary>
    [ObservableProperty] private decimal _tareWeight;

    /// <summary>
    /// 净重（吨）
    /// </summary>
    [ObservableProperty] private decimal _netWeight;

    /// <summary>
    /// 材料类别
    /// </summary>
    [ObservableProperty] private string? _materialCategory;

    /// <summary>
    /// 备注
    /// </summary>
    [ObservableProperty] private string? _remark;

    /// <summary>
    /// 材料列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<Material> _materials = new();

    /// <summary>
    /// 选中的材料
    /// </summary>
    [ObservableProperty] private Material? _selectedMaterial;

    /// <summary>
    /// 材料单位列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<MaterialUnitDto> _materialUnits = new();

    /// <summary>
    /// 选中的材料单位
    /// </summary>
    [ObservableProperty] private MaterialUnitDto? _selectedMaterialUnit;

    /// <summary>
    /// 规格型号（只读，从材料获取）
    /// </summary>
    [ObservableProperty] private string? _specifications;

    /// <summary>
    /// 单位名称（只读）
    /// </summary>
    [ObservableProperty] private string? _unitName;

    /// <summary>
    /// 换算系数
    /// </summary>
    [ObservableProperty] private decimal _conversionRate = 1.00m;

    /// <summary>
    /// 运单数量
    /// </summary>
    [ObservableProperty] private string? _waybillQuantity;

    /// <summary>
    /// 运单重量（只读，自动计算）
    /// </summary>
    [ObservableProperty] private decimal _waybillWeight;

    /// <summary>
    /// 进场照片列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _entryPhotos = new();

    /// <summary>
    /// 出场照片列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _exitPhotos = new();

    /// <summary>
    /// 运单照片
    /// </summary>
    [ObservableProperty] private string? _ticketPhoto;

    /// <summary>
    /// 是否正在加载
    /// </summary>
    [ObservableProperty] private bool _isLoading;

    /// <summary>
    /// 收发料类型
    /// </summary>
    public DeliveryType DeliveryType => _deliveryType;

    /// <summary>
    /// 当前记录（只读）
    /// </summary>
    public WeighingRecord CurrentRecord => _currentRecord;

    /// <summary>
    /// 匹配记录（只读）
    /// </summary>
    public WeighingRecord MatchedRecord => _matchedRecord;

    #endregion

    public ManualMatchEditWindowViewModel(
        WeighingRecord currentRecord,
        WeighingRecord matchedRecord,
        DeliveryType deliveryType,
        IServiceProvider serviceProvider)
    {
        _currentRecord = currentRecord;
        _matchedRecord = matchedRecord;
        _deliveryType = deliveryType;
        _serviceProvider = serviceProvider;

        // 获取仓储服务
        _providerRepository = serviceProvider.GetService<IRepository<Provider, int>>();
        _materialRepository = serviceProvider.GetService<IRepository<Material, int>>();
        _materialUnitRepository = serviceProvider.GetService<IRepository<MaterialUnit, int>>();
        _attachmentRepository = serviceProvider.GetService<IRepository<WeighingRecordAttachment, int>>();
        _weighingRecordRepository = serviceProvider.GetService<IRepository<WeighingRecord, long>>();

        // 初始化数据
        InitializeData();

        // 监听材料选择变化，加载对应的单位列表
        this.WhenAnyValue(x => x.SelectedMaterial)
            .Subscribe(async material =>
            {
                if (material != null)
                {
                    Specifications = material.Specifications ?? material.Size;
                    MaterialCategory = material.Name;
                    await LoadMaterialUnitsAsync(material.Id);
                }
                else
                {
                    Specifications = null;
                    MaterialUnits.Clear();
                    SelectedMaterialUnit = null;
                }
            });

        // 监听材料单位变化，更新单位名称和换算系数
        this.WhenAnyValue(x => x.SelectedMaterialUnit)
            .Subscribe(unit =>
            {
                if (unit != null)
                {
                    UnitName = unit.UnitName;
                    ConversionRate = unit.Rate;
                    UpdateWaybillWeight();
                }
                else
                {
                    UnitName = null;
                    ConversionRate = 1.00m;
                }
            });

        // 加载下拉列表数据和照片
        _ = LoadDropdownDataAsync();
        _ = LoadPhotosAsync();
    }

    #region 初始化

    private void InitializeData()
    {
        // 设置车牌号（优先使用当前记录的车牌号）
        PlateNumber = _currentRecord.PlateNumber ?? _matchedRecord.PlateNumber;

        // 根据收发类型确定毛重和皮重
        // 收料（Receiving）：进场时车带货物是毛重，出场时空车是皮重
        // 发料（Sending）：进场时空车是皮重，出场时车带货物是毛重
        if (_deliveryType == DeliveryType.Receiving)
        {
            // 收料：currentRecord 是先进场的（毛重），matchedRecord 是后出场的（皮重）
            // 但需要根据时间判断哪个是进场哪个是出场
            if (_currentRecord.CreationTime <= _matchedRecord.CreationTime)
            {
                // currentRecord 先发生，是进场（毛重）
                GrossWeight = _currentRecord.Weight;
                GrossWeightTime = _currentRecord.CreationTime;
                TareWeight = _matchedRecord.Weight;
                TareWeightTime = _matchedRecord.CreationTime;
            }
            else
            {
                // matchedRecord 先发生，是进场（毛重）
                GrossWeight = _matchedRecord.Weight;
                GrossWeightTime = _matchedRecord.CreationTime;
                TareWeight = _currentRecord.Weight;
                TareWeightTime = _currentRecord.CreationTime;
            }
        }
        else
        {
            // 发料：进场时空车是皮重，出场时车带货物是毛重
            if (_currentRecord.CreationTime <= _matchedRecord.CreationTime)
            {
                // currentRecord 先发生，是进场（皮重）
                TareWeight = _currentRecord.Weight;
                TareWeightTime = _currentRecord.CreationTime;
                GrossWeight = _matchedRecord.Weight;
                GrossWeightTime = _matchedRecord.CreationTime;
            }
            else
            {
                // matchedRecord 先发生，是进场（皮重）
                TareWeight = _matchedRecord.Weight;
                TareWeightTime = _matchedRecord.CreationTime;
                GrossWeight = _currentRecord.Weight;
                GrossWeightTime = _currentRecord.CreationTime;
            }
        }

        // 计算净重
        NetWeight = Math.Abs(GrossWeight - TareWeight);

        // 初始化运单数量为净重
        WaybillQuantity = NetWeight.ToString("F2");
        WaybillWeight = NetWeight;
    }

    private async Task LoadDropdownDataAsync()
    {
        try
        {
            IsLoading = true;

            await Task.WhenAll(
                LoadProvidersAsync(),
                LoadMaterialsAsync()
            );

            // 设置已选中的供应商（如果有）
            var providerId = _currentRecord.ProviderId ?? _matchedRecord.ProviderId;
            if (providerId.HasValue)
            {
                SelectedProvider = Providers.FirstOrDefault(p => p.Id == providerId.Value);
            }

            // 设置已选中的材料（如果有）
            var materialId = _currentRecord.MaterialId ?? _matchedRecord.MaterialId;
            if (materialId.HasValue)
            {
                SelectedMaterial = Materials.FirstOrDefault(m => m.Id == materialId.Value);

                if (SelectedMaterial != null)
                {
                    await LoadMaterialUnitsAsync(SelectedMaterial.Id);

                    var materialUnitId = _currentRecord.MaterialUnitId ?? _matchedRecord.MaterialUnitId;
                    if (materialUnitId.HasValue)
                    {
                        SelectedMaterialUnit = MaterialUnits.FirstOrDefault(u => u.Id == materialUnitId.Value);
                    }
                }
            }

            // 设置运单数量（如果有）
            var waybillQuantity = _currentRecord.WaybillQuantity ?? _matchedRecord.WaybillQuantity;
            if (waybillQuantity.HasValue)
            {
                WaybillQuantity = waybillQuantity.Value.ToString("F2");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载下拉数据失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadProvidersAsync()
    {
        if (_providerRepository == null) return;

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
            System.Diagnostics.Debug.WriteLine($"加载供应商失败: {ex.Message}");
        }
    }

    private async Task LoadMaterialsAsync()
    {
        if (_materialRepository == null) return;

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
            System.Diagnostics.Debug.WriteLine($"加载材料失败: {ex.Message}");
        }
    }

    private async Task LoadMaterialUnitsAsync(int materialId)
    {
        if (_materialUnitRepository == null) return;

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
                    Rate = unit.Rate ?? 1m,
                    RateName = unit.RateName,
                    ProviderId = unit.ProviderId
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载材料单位失败: {ex.Message}");
        }
    }

    private async Task LoadPhotosAsync()
    {
        if (_attachmentRepository == null) return;

        try
        {
            // 加载当前记录的附件
            var currentAttachments = await _attachmentRepository.GetListAsync(
                predicate: x => x.WeighingRecordId == _currentRecord.Id,
                includeDetails: true
            );

            // 加载匹配记录的附件
            var matchedAttachments = await _attachmentRepository.GetListAsync(
                predicate: x => x.WeighingRecordId == _matchedRecord.Id,
                includeDetails: true
            );

            EntryPhotos.Clear();
            ExitPhotos.Clear();
            TicketPhoto = null;

            // 根据时间判断哪些是进场照片，哪些是出场照片
            var earlierRecord = _currentRecord.CreationTime <= _matchedRecord.CreationTime ? _currentRecord : _matchedRecord;
            var laterRecord = _currentRecord.CreationTime > _matchedRecord.CreationTime ? _currentRecord : _matchedRecord;
            var earlierAttachments = earlierRecord.Id == _currentRecord.Id ? currentAttachments : matchedAttachments;
            var laterAttachments = laterRecord.Id == _currentRecord.Id ? currentAttachments : matchedAttachments;

            // 进场照片（较早记录的照片）
            foreach (var attachment in earlierAttachments)
            {
                if (attachment.AttachmentFile != null && !string.IsNullOrEmpty(attachment.AttachmentFile.LocalPath))
                {
                    if (attachment.AttachmentFile.AttachType == AttachType.EntryPhoto)
                    {
                        EntryPhotos.Add(attachment.AttachmentFile.LocalPath);
                    }
                    else if (attachment.AttachmentFile.AttachType == AttachType.TicketPhoto && TicketPhoto == null)
                    {
                        TicketPhoto = attachment.AttachmentFile.LocalPath;
                    }
                }
            }

            // 出场照片（较晚记录的照片）
            foreach (var attachment in laterAttachments)
            {
                if (attachment.AttachmentFile != null && !string.IsNullOrEmpty(attachment.AttachmentFile.LocalPath))
                {
                    if (attachment.AttachmentFile.AttachType == AttachType.EntryPhoto ||
                        attachment.AttachmentFile.AttachType == AttachType.ExitPhoto)
                    {
                        ExitPhotos.Add(attachment.AttachmentFile.LocalPath);
                    }
                    else if (attachment.AttachmentFile.AttachType == AttachType.TicketPhoto && TicketPhoto == null)
                    {
                        TicketPhoto = attachment.AttachmentFile.LocalPath;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载照片失败: {ex.Message}");
        }
    }

    #endregion

    #region 命令

    /// <summary>
    /// 保存命令
    /// </summary>
    [RelayCommand]
    public async Task<bool> SaveAsync()
    {
        if (_weighingRecordRepository == null) return false;

        try
        {
            // 解析运单数量
            decimal? waybillQuantity = null;
            if (!string.IsNullOrWhiteSpace(WaybillQuantity) &&
                decimal.TryParse(WaybillQuantity, out var qty))
            {
                waybillQuantity = qty;
            }

            // 更新当前记录
            _currentRecord.Update(
                PlateNumber,
                SelectedProvider?.Id,
                SelectedMaterial?.Id,
                waybillQuantity
            );
            _currentRecord.MaterialUnitId = SelectedMaterialUnit?.Id;
            _currentRecord.DeliveryType = _deliveryType;

            // 设置匹配关系
            if (_deliveryType == DeliveryType.Receiving)
            {
                // 收料：当前记录是进场（Join），匹配记录是出场（Out）
                if (_currentRecord.CreationTime <= _matchedRecord.CreationTime)
                {
                    _currentRecord.MatchAsJoin(_matchedRecord.Id);
                    _matchedRecord.MatchAsOut(_currentRecord.Id);
                }
                else
                {
                    _currentRecord.MatchAsOut(_matchedRecord.Id);
                    _matchedRecord.MatchAsJoin(_currentRecord.Id);
                }
            }
            else
            {
                // 发料：当前记录是进场（Join-皮重），匹配记录是出场（Out-毛重）
                if (_currentRecord.CreationTime <= _matchedRecord.CreationTime)
                {
                    _currentRecord.MatchAsJoin(_matchedRecord.Id);
                    _matchedRecord.MatchAsOut(_currentRecord.Id);
                }
                else
                {
                    _currentRecord.MatchAsOut(_matchedRecord.Id);
                    _matchedRecord.MatchAsJoin(_currentRecord.Id);
                }
            }

            // 更新匹配记录
            _matchedRecord.Update(
                PlateNumber,
                SelectedProvider?.Id,
                SelectedMaterial?.Id,
                waybillQuantity
            );
            _matchedRecord.MaterialUnitId = SelectedMaterialUnit?.Id;
            _matchedRecord.DeliveryType = _deliveryType;

            // 保存到数据库
            await _weighingRecordRepository.UpdateAsync(_currentRecord);
            await _weighingRecordRepository.UpdateAsync(_matchedRecord);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 添加材料命令
    /// </summary>
    [RelayCommand]
    private void AddMaterial()
    {
        // TODO: 实现添加材料逻辑
    }

    /// <summary>
    /// 删除材料命令
    /// </summary>
    [RelayCommand]
    private void DeleteMaterial()
    {
        // TODO: 实现删除材料逻辑
    }

    /// <summary>
    /// 拍照命令
    /// </summary>
    [RelayCommand]
    private void TakePhoto()
    {
        // TODO: 实现拍照逻辑
    }

    #endregion

    #region 辅助方法

    private void UpdateWaybillWeight()
    {
        if (!string.IsNullOrWhiteSpace(WaybillQuantity) &&
            decimal.TryParse(WaybillQuantity, out var qty))
        {
            WaybillWeight = qty * ConversionRate;
        }
    }

    partial void OnWaybillQuantityChanged(string? value)
    {
        UpdateWaybillWeight();
    }

    #endregion
}
