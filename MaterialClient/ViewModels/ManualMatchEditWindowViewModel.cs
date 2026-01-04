using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.ViewModels;

/// <summary>
///     手动匹配编辑窗口 ViewModel
/// </summary>
public partial class ManualMatchEditWindowViewModel : ViewModelBase, ITransientDependency
{
    private readonly IMaterialService? _materialService;
    private readonly IServiceProvider _serviceProvider;

    public ManualMatchEditWindowViewModel(
        WeighingRecord currentRecord,
        WeighingRecord matchedRecord,
        DeliveryType deliveryType,
        IServiceProvider serviceProvider)
        : base(serviceProvider.GetService<ILogger<ManualMatchEditWindowViewModel>>())
    {
        CurrentRecord = currentRecord;
        MatchedRecord = matchedRecord;
        DeliveryType = deliveryType;
        _serviceProvider = serviceProvider;

        _materialService = serviceProvider.GetService<IMaterialService>();

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

        this.WhenAnyValue(x => x.WaybillQuantity)
            .Subscribe(_ => UpdateWaybillWeight());

        _ = LoadDropdownDataAsync();
        _ = LoadPhotosAsync();
    }

    #region 辅助方法

    private void UpdateWaybillWeight()
    {
        if (!string.IsNullOrWhiteSpace(WaybillQuantity) &&
            decimal.TryParse(WaybillQuantity, out var qty))
            WaybillWeight = qty * ConversionRate;
    }

    #endregion

    #region 属性

    [Reactive] private string? _plateNumber;

    [Reactive] private ObservableCollection<ProviderDto> _providers = new();

    [Reactive] private ProviderDto? _selectedProvider;

    [Reactive] private DateTime? _grossWeightTime;

    [Reactive] private DateTime? _tareWeightTime;

    [Reactive] private decimal _grossWeight;

    [Reactive] private decimal _tareWeight;

    [Reactive] private decimal _netWeight;

    [Reactive] private string? _materialCategory;

    [Reactive] private string? _remark;

    [Reactive] private ObservableCollection<Material> _materials = new();

    [Reactive] private Material? _selectedMaterial;

    [Reactive] private ObservableCollection<MaterialUnitDto> _materialUnits = new();

    [Reactive] private MaterialUnitDto? _selectedMaterialUnit;

    [Reactive] private string? _specifications;

    [Reactive] private string? _unitName;

    [Reactive] private decimal _conversionRate = 1.00m;

    [Reactive] private string? _waybillQuantity;

    [Reactive] private decimal _waybillWeight;

    [Reactive] private ObservableCollection<string> _entryPhotos = new();

    [Reactive] private ObservableCollection<string> _exitPhotos = new();

    [Reactive] private string? _ticketPhoto;

    [Reactive] private bool _isLoading;

    public DeliveryType DeliveryType { get; }

    public WeighingRecord CurrentRecord { get; }

    public WeighingRecord MatchedRecord { get; }

    #endregion

    #region 初始化

    private void InitializeData()
    {
        PlateNumber = CurrentRecord.PlateNumber ?? MatchedRecord.PlateNumber;

        if (DeliveryType == DeliveryType.Receiving)
        {
            if (CurrentRecord.AddDate <= MatchedRecord.AddDate)
            {
                GrossWeight = CurrentRecord.TotalWeight;
                GrossWeightTime = CurrentRecord.AddDate;
                TareWeight = MatchedRecord.TotalWeight;
                TareWeightTime = MatchedRecord.AddDate;
            }
            else
            {
                GrossWeight = MatchedRecord.TotalWeight;
                GrossWeightTime = MatchedRecord.AddDate;
                TareWeight = CurrentRecord.TotalWeight;
                TareWeightTime = CurrentRecord.AddDate;
            }
        }
        else
        {
            if (CurrentRecord.AddDate <= MatchedRecord.AddDate)
            {
                TareWeight = CurrentRecord.TotalWeight;
                TareWeightTime = CurrentRecord.AddDate;
                GrossWeight = MatchedRecord.TotalWeight;
                GrossWeightTime = MatchedRecord.AddDate;
            }
            else
            {
                TareWeight = MatchedRecord.TotalWeight;
                TareWeightTime = MatchedRecord.AddDate;
                GrossWeight = CurrentRecord.TotalWeight;
                GrossWeightTime = CurrentRecord.AddDate;
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

            var providerId = CurrentRecord.ProviderId ?? MatchedRecord.ProviderId;
            if (providerId.HasValue) SelectedProvider = Providers.FirstOrDefault(p => p.Id == providerId.Value);

            // 从 Materials 集合获取物料信息
            var currentMaterial = CurrentRecord.Materials?.FirstOrDefault();
            var matchedMaterial = MatchedRecord.Materials?.FirstOrDefault();
            var materialId = currentMaterial?.MaterialId ?? matchedMaterial?.MaterialId;
            if (materialId.HasValue)
            {
                SelectedMaterial = Materials.FirstOrDefault(m => m.Id == materialId.Value);

                if (SelectedMaterial != null)
                {
                    await LoadMaterialUnitsAsync(SelectedMaterial.Id);

                    var materialUnitId = currentMaterial?.MaterialUnitId ?? matchedMaterial?.MaterialUnitId;
                    if (materialUnitId.HasValue)
                        SelectedMaterialUnit = MaterialUnits.FirstOrDefault(u => u.Id == materialUnitId.Value);
                }
            }

            var waybillQuantity = currentMaterial?.WaybillQuantity ?? matchedMaterial?.WaybillQuantity;
            if (waybillQuantity.HasValue) WaybillQuantity = waybillQuantity.Value.ToString("F2");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载下拉数据失败");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadProvidersAsync()
    {
        if (_materialService == null) return;

        try
        {
            var providers = await _materialService.GetAllProvidersAsync();
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
            Logger?.LogError(ex, "加载供应商失败");
        }
    }

    private async Task LoadMaterialsAsync()
    {
        if (_materialService == null) return;

        try
        {
            var materials = await _materialService.GetAllMaterialsAsync();
            Materials.Clear();
            foreach (var material in materials) Materials.Add(material);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载材料失败");
        }
    }

    private async Task LoadMaterialUnitsAsync(int materialId)
    {
        if (_materialService == null) return;

        try
        {
            var units = await _materialService.GetMaterialUnitsByMaterialIdAsync(materialId);
            MaterialUnits.Clear();
            foreach (var unit in units)
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
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载材料单位失败");
        }
    }

    private async Task LoadPhotosAsync()
    {
        var attachmentService = _serviceProvider.GetService<IAttachmentService>();
        if (attachmentService == null) return;

        try
        {
            var attachmentsDict = await attachmentService.GetAttachmentsByWeighingRecordIdsAsync(
                new[] { CurrentRecord.Id, MatchedRecord.Id });

            attachmentsDict.TryGetValue(CurrentRecord.Id, out var currentFiles);
            attachmentsDict.TryGetValue(MatchedRecord.Id, out var matchedFiles);

            EntryPhotos.Clear();
            ExitPhotos.Clear();
            TicketPhoto = null;

            var earlierRecord = CurrentRecord.AddDate <= MatchedRecord.AddDate
                ? CurrentRecord
                : MatchedRecord;
            var earlierFiles = earlierRecord.Id == CurrentRecord.Id ? currentFiles : matchedFiles;
            var laterFiles = earlierRecord.Id == CurrentRecord.Id ? matchedFiles : currentFiles;

            if (earlierFiles != null)
                foreach (var file in earlierFiles)
                    if (!string.IsNullOrEmpty(file.LocalPath))
                    {
                        if (file.AttachType == AttachType.EntryPhoto ||
                            file.AttachType == AttachType.UnmatchedEntryPhoto)
                            EntryPhotos.Add(file.LocalPath);
                        else if (file.AttachType == AttachType.TicketPhoto && TicketPhoto == null)
                            TicketPhoto = file.LocalPath;
                    }

            if (laterFiles != null)
                foreach (var file in laterFiles)
                    if (!string.IsNullOrEmpty(file.LocalPath))
                    {
                        if (file.AttachType == AttachType.EntryPhoto ||
                            file.AttachType == AttachType.ExitPhoto ||
                            file.AttachType == AttachType.UnmatchedEntryPhoto)
                            ExitPhotos.Add(file.LocalPath);
                        else if (file.AttachType == AttachType.TicketPhoto && TicketPhoto == null)
                            TicketPhoto = file.LocalPath;
                    }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载照片失败");
        }
    }

    #endregion

    #region 命令

    [ReactiveCommand]
    public async Task<bool> SaveAsync()
    {
        var matchingService = _serviceProvider.GetService<IWeighingMatchingService>();
        if (matchingService == null) return false;

        try
        {
            // 先更新两条记录的基本信息和物料信息
            decimal? waybillQuantity = null;
            if (!string.IsNullOrWhiteSpace(WaybillQuantity) &&
                decimal.TryParse(WaybillQuantity, out var qty))
                waybillQuantity = qty;

            CurrentRecord.Update(PlateNumber, SelectedProvider?.Id);
            CurrentRecord.DeliveryType = DeliveryType;
            UpdateRecordMaterial(CurrentRecord, SelectedMaterial?.Id, SelectedMaterialUnit?.Id, waybillQuantity);

            MatchedRecord.Update(PlateNumber, SelectedProvider?.Id);
            MatchedRecord.DeliveryType = DeliveryType;
            UpdateRecordMaterial(MatchedRecord, SelectedMaterial?.Id, SelectedMaterialUnit?.Id, waybillQuantity);

            // 调用 ManualMatchAsync 执行匹配和创建运单
            var waybill = await matchingService.ManualMatchAsync(CurrentRecord, MatchedRecord, DeliveryType);

            // 发送匹配成功消息
            var message = new MatchSucceededMessage(waybill.Id, CurrentRecord.Id);
            MessageBus.Current.SendMessage(message);
            Logger?.LogInformation(
                "SaveAsync: Sent MatchSucceededMessage via MessageBus for WaybillId {WaybillId}, WeighingRecordId {RecordId}",
                waybill.Id, CurrentRecord.Id);

            return true;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "保存失败");
            return false;
        }
    }

    private void UpdateRecordMaterial(WeighingRecord record, int? materialId, int? materialUnitId,
        decimal? waybillQuantity)
    {
        var materials = record.Materials;
        var firstMaterial = materials.FirstOrDefault();

        if (firstMaterial != null)
        {
            firstMaterial.MaterialId = materialId;
            firstMaterial.MaterialUnitId = materialUnitId;
            firstMaterial.WaybillQuantity = waybillQuantity;
            // 重新设置以触发 JSON 序列化
            record.Materials = materials;
        }
        else if (materialId.HasValue || materialUnitId.HasValue || waybillQuantity.HasValue)
        {
            // 如果没有现有物料记录但需要设置值，创建新的
            var newMaterial = new WeighingRecordMaterial(
                0, // Weight 将在业务逻辑中设置
                materialId,
                materialUnitId,
                waybillQuantity);
            record.AddMaterial(newMaterial);
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
}