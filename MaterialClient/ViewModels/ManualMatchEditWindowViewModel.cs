using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IRepository<WeighingRecord, long>? _weighingRecordRepository;

    #region 属性

    [Reactive]
    private string? _plateNumber;

    [Reactive]
    private ObservableCollection<ProviderDto> _providers = new();

    [Reactive]
    private ProviderDto? _selectedProvider;

    [Reactive]
    private DateTime? _grossWeightTime;

    [Reactive]
    private DateTime? _tareWeightTime;

    [Reactive]
    private decimal _grossWeight;

    [Reactive]
    private decimal _tareWeight;

    [Reactive]
    private decimal _netWeight;

    [Reactive]
    private string? _materialCategory;

    [Reactive]
    private string? _remark;

    [Reactive]
    private ObservableCollection<Material> _materials = new();

    [Reactive]
    private Material? _selectedMaterial;

    [Reactive]
    private ObservableCollection<MaterialUnitDto> _materialUnits = new();

    [Reactive]
    private MaterialUnitDto? _selectedMaterialUnit;

    [Reactive]
    private string? _specifications;

    [Reactive]
    private string? _unitName;

    [Reactive]
    private decimal _conversionRate = 1.00m;

    [Reactive]
    private string? _waybillQuantity;

    [Reactive]
    private decimal _waybillWeight;

    [Reactive]
    private ObservableCollection<string> _entryPhotos = new();

    [Reactive]
    private ObservableCollection<string> _exitPhotos = new();

    [Reactive]
    private string? _ticketPhoto;

    [Reactive]
    private bool _isLoading;

    public DeliveryType DeliveryType => _deliveryType;
    public WeighingRecord CurrentRecord => _currentRecord;
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

        _providerRepository = serviceProvider.GetService<IRepository<Provider, int>>();
        _materialRepository = serviceProvider.GetService<IRepository<Material, int>>();
        _materialUnitRepository = serviceProvider.GetService<IRepository<MaterialUnit, int>>();
        _weighingRecordRepository = serviceProvider.GetService<IRepository<WeighingRecord, long>>();

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

        _ = LoadDropdownDataAsync();
        _ = LoadPhotosAsync();
    }

    partial void OnWaybillQuantityChanged(string? value)
    {
        UpdateWaybillWeight();
    }

    #region 初始化

    private void InitializeData()
    {
        PlateNumber = _currentRecord.PlateNumber ?? _matchedRecord.PlateNumber;

        if (_deliveryType == DeliveryType.Receiving)
        {
            if (_currentRecord.CreationTime <= _matchedRecord.CreationTime)
            {
                GrossWeight = _currentRecord.Weight;
                GrossWeightTime = _currentRecord.CreationTime;
                TareWeight = _matchedRecord.Weight;
                TareWeightTime = _matchedRecord.CreationTime;
            }
            else
            {
                GrossWeight = _matchedRecord.Weight;
                GrossWeightTime = _matchedRecord.CreationTime;
                TareWeight = _currentRecord.Weight;
                TareWeightTime = _currentRecord.CreationTime;
            }
        }
        else
        {
            if (_currentRecord.CreationTime <= _matchedRecord.CreationTime)
            {
                TareWeight = _currentRecord.Weight;
                TareWeightTime = _currentRecord.CreationTime;
                GrossWeight = _matchedRecord.Weight;
                GrossWeightTime = _matchedRecord.CreationTime;
            }
            else
            {
                TareWeight = _matchedRecord.Weight;
                TareWeightTime = _matchedRecord.CreationTime;
                GrossWeight = _currentRecord.Weight;
                GrossWeightTime = _currentRecord.CreationTime;
            }
        }

        NetWeight = Math.Abs(GrossWeight - TareWeight);
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

            var providerId = _currentRecord.ProviderId ?? _matchedRecord.ProviderId;
            if (providerId.HasValue)
            {
                SelectedProvider = Providers.FirstOrDefault(p => p.Id == providerId.Value);
            }

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
        var attachmentService = _serviceProvider.GetService<IAttachmentService>();
        if (attachmentService == null) return;

        try
        {
            var attachmentsDict = await attachmentService.GetAttachmentsByWeighingRecordIdsAsync(
                new[] { _currentRecord.Id, _matchedRecord.Id });

            attachmentsDict.TryGetValue(_currentRecord.Id, out var currentFiles);
            attachmentsDict.TryGetValue(_matchedRecord.Id, out var matchedFiles);

            EntryPhotos.Clear();
            ExitPhotos.Clear();
            TicketPhoto = null;

            var earlierRecord = _currentRecord.CreationTime <= _matchedRecord.CreationTime
                ? _currentRecord
                : _matchedRecord;
            var earlierFiles = earlierRecord.Id == _currentRecord.Id ? currentFiles : matchedFiles;
            var laterFiles = earlierRecord.Id == _currentRecord.Id ? matchedFiles : currentFiles;

            if (earlierFiles != null)
            {
                foreach (var file in earlierFiles)
                {
                    if (!string.IsNullOrEmpty(file.LocalPath))
                    {
                        if (file.AttachType == AttachType.EntryPhoto)
                        {
                            EntryPhotos.Add(file.LocalPath);
                        }
                        else if (file.AttachType == AttachType.TicketPhoto && TicketPhoto == null)
                        {
                            TicketPhoto = file.LocalPath;
                        }
                    }
                }
            }

            if (laterFiles != null)
            {
                foreach (var file in laterFiles)
                {
                    if (!string.IsNullOrEmpty(file.LocalPath))
                    {
                        if (file.AttachType == AttachType.EntryPhoto ||
                            file.AttachType == AttachType.ExitPhoto)
                        {
                            ExitPhotos.Add(file.LocalPath);
                        }
                        else if (file.AttachType == AttachType.TicketPhoto && TicketPhoto == null)
                        {
                            TicketPhoto = file.LocalPath;
                        }
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

    [ReactiveCommand]
    public async Task<bool> SaveAsync()
    {
        if (_weighingRecordRepository == null) return false;

        try
        {
            decimal? waybillQuantity = null;
            if (!string.IsNullOrWhiteSpace(WaybillQuantity) &&
                decimal.TryParse(WaybillQuantity, out var qty))
            {
                waybillQuantity = qty;
            }

            _currentRecord.Update(
                PlateNumber,
                SelectedProvider?.Id,
                SelectedMaterial?.Id,
                SelectedMaterialUnit?.Id,
                waybillQuantity
            );
            _currentRecord.DeliveryType = _deliveryType;

            if (_deliveryType == DeliveryType.Receiving)
            {
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

            _matchedRecord.Update(
                PlateNumber,
                SelectedProvider?.Id,
                SelectedMaterial?.Id,
                SelectedMaterialUnit?.Id,
                waybillQuantity
            );
            _matchedRecord.DeliveryType = _deliveryType;

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

    [ReactiveCommand]
    private void AddMaterial()
    {
        // TODO: 实现添加材料逻辑
    }

    [ReactiveCommand]
    private void DeleteMaterial()
    {
        // TODO: 实现删除材料逻辑
    }

    [ReactiveCommand]
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

    #endregion
}
