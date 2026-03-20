using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Configuration;
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
using Microsoft.Extensions.Options;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MaterialClient.ViewModels;

/// <summary>
///     固废模式详情窗口 ViewModel
/// </summary>
public partial class SolidWasteModeDetailViewModel : AttendedWeighingDetailViewModelBase, ITransientDependency
{
    private readonly IMaterialService _materialService;
    private readonly IProviderService _providerService;
    private readonly IOptions<StreetsConfig> _streetsConfig;
    private readonly IOptions<SolidWasteTypeConfig> _solidWasteTypeConfig;
    private readonly ISettingsService _settingsService;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;

    public SolidWasteModeDetailViewModel(
        IServiceProvider serviceProvider)
        : base(serviceProvider, serviceProvider.GetService<ILogger<SolidWasteModeDetailViewModel>>())
    {
        _materialService = serviceProvider.GetRequiredService<IMaterialService>();
        _providerService = serviceProvider.GetRequiredService<IProviderService>();
        _streetsConfig = serviceProvider.GetRequiredService<IOptions<StreetsConfig>>();
        _solidWasteTypeConfig = serviceProvider.GetRequiredService<IOptions<SolidWasteTypeConfig>>();
        _settingsService = serviceProvider.GetRequiredService<ISettingsService>();
        _weighingRecordRepository = serviceProvider.GetRequiredService<IRepository<WeighingRecord, long>>();

        // 初始化 SolidWaste 下拉选择弹窗（镇街/材料/供应商）
        InitializeSolidWasteSelectionPopups();

        // SolidWaste 模式：材料选择时自动选择第一个单位
        this.WhenAnyValue(x => x.SelectedSolidWasteMaterial)
            .Where(material => material != null && IsSolidWasteMode)
            .Subscribe(async material =>
            {
                if (material != null)
                {
                    try
                    {
                        var units = await LoadMaterialUnitsForRowAsync(material.Id);
                        if (units.Count > 0 && MaterialItems.Count > 0)
                        {
                            var firstRow = MaterialItems[0];
                            firstRow.SetMaterialUnits(units);
                            firstRow.InitializeSelection(material, units, units[0].Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "自动选择材料单位失败");
                    }
                }
            });

        // SolidWaste 模式：运单数量 = 实际重量（GoodsWeight）
        this.WhenAnyValue(x => x.GoodsWeight, x => x.IsSolidWasteMode)
            .Where(tuple => tuple.Item2) // 仅在 SolidWaste 模式下
            .Subscribe(tuple =>
            {
                if (IsSolidWasteMode && MaterialItems.Count > 0)
                {
                    var firstRow = MaterialItems[0];
                    firstRow.WaybillQuantity = GoodsWeight;
                }
            });
    }

    #region 固废模式专用属性

    [Reactive] private string? _solidWasteOrderNumber;

    [Reactive] private ObservableCollection<string> _streets = new();

    [Reactive] private string? _selectedStreet;

    [Reactive] private ObservableCollection<string> _solidWasteTypes = new();

    [Reactive] private string? _selectedSolidWasteType;

    [Reactive] private ObservableCollection<Material> _solidWasteMaterials = new();

    [Reactive] private Material? _selectedSolidWasteMaterial;

    // SolidWaste 模式：增强下拉选择弹窗（搜索/分页)
    [Reactive] private GenericSelectionPopupViewModel<string>? _streetsPopupViewModel;
    [Reactive] private GenericSelectionPopupViewModel<Material>? _materialsPopupViewModel;
    [Reactive] private GenericSelectionPopupViewModel<ProviderDto>? _providersPopupViewModel;

    [Reactive] private bool _isStreetsPopupOpen;
    [Reactive] private bool _isMaterialsPopupOpen;
    [Reactive] private bool _isProvidersPopupOpen;

    // 供应商列表（用于标准下拉）
    [Reactive] private ObservableCollection<ProviderDto> _providers = new();

    [Reactive] private ProviderDto? _selectedProvider;

    #endregion

    #region 初始化

    private void InitializeSolidWasteSelectionPopups()
    {
        // 镇街：客户端分页
        StreetsPopupViewModel = new GenericSelectionPopupViewModel<string>(
            pagingMode: GenericSelectionPagingMode.ClientSide,
            displayTextSelector: s => s,
            logger: Logger,
            loadAllFunc: () =>
            {
                var streets = _streetsConfig.Value.Streets ?? Array.Empty<string>();
                System.Collections.Generic.IReadOnlyList<string> result = streets
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();
                return Task.FromResult<System.Collections.Generic.IReadOnlyList<string>>(result);
            },
            allowAddNew: false);

        _ = StreetsPopupViewModel.InitializeAsync();

        StreetsPopupViewModel.WhenAnyValue(x => x.SelectedItem)
            .Where(item => item != null)
            .Subscribe(item =>
            {
                if (item == null) return;
                SelectedStreet = item.Value;
                IsStreetsPopupOpen = false;
            });

        this.WhenAnyValue(x => x.IsStreetsPopupOpen)
            .Subscribe(isOpen =>
            {
                if (isOpen && StreetsPopupViewModel != null)
                {
                    StreetsPopupViewModel.SearchText = string.Empty;
                    StreetsPopupViewModel.CurrentPage = 1;

                    if (!string.IsNullOrEmpty(SelectedStreet))
                    {
                        StreetsPopupViewModel.SelectedItem = new GenericSelectionItem<string>
                        {
                            Value = SelectedStreet,
                            DisplayText = SelectedStreet
                        };
                    }
                    else
                    {
                        StreetsPopupViewModel.SelectedItem = null;
                    }

                    _ = StreetsPopupViewModel.RefreshAsync();
                }
            });

        // 材料：服务端分页（支持按搜索新增）
        MaterialsPopupViewModel = new GenericSelectionPopupViewModel<Material>(
            pagingMode: GenericSelectionPagingMode.ServerSide,
            displayTextSelector: m => m.Name ?? string.Empty,
            logger: Logger,
            loadPageFunc: (search, pageIndex, pageSize, selectedIds) =>
                _materialService.GetPagedMaterialsAsync(search, pageIndex, pageSize, selectedIds),
            getSelectedId: m => m.Id,
            createNewItemFunc: async name =>
                (Material?)await _materialService.CreateMaterialAsync(name),
            confirmNewNameFunc: async proposed =>
                await ConfirmTextInteraction.Handle(new ConfirmTextRequest(
                        Title: "确认新增材料",
                        Message: "将新增一条材料，请确认名称：",
                        InitialValue: proposed)));

        _ = MaterialsPopupViewModel.InitializeAsync();

        var wasMaterialsPopupOpen = false;
        this.WhenAnyValue(x => x.IsMaterialsPopupOpen, x => x.MaterialsPopupViewModel.SelectedItem)
            .Subscribe(tuple =>
            {
                var (isOpen, selectedItem) = tuple;
                if (MaterialsPopupViewModel == null) return;

                // 1) 先处理"弹窗刚打开"
                if (isOpen && !wasMaterialsPopupOpen)
                {
                    wasMaterialsPopupOpen = true;
                    MaterialsPopupViewModel.SearchText = string.Empty;
                    MaterialsPopupViewModel.CurrentPage = 1;
                    MaterialsPopupViewModel.PendingSelectedIds = SelectedSolidWasteMaterial != null
                        ? new System.Collections.Generic.List<int> { SelectedSolidWasteMaterial.Id }
                        : null;
                    if (SelectedSolidWasteMaterial != null)
                    {
                        MaterialsPopupViewModel.SelectedItem = new GenericSelectionItem<Material>
                        {
                            Value = SelectedSolidWasteMaterial,
                            DisplayText = SelectedSolidWasteMaterial.Name ?? string.Empty
                        };
                    }
                    else
                    {
                        MaterialsPopupViewModel.SelectedItem = null;
                    }
                }

                if (!isOpen)
                {
                    wasMaterialsPopupOpen = false;
                    return;
                }

                // 2) 再处理"选中项与当前不同 → 回写并关弹窗"
                if (selectedItem == null) return;

                if (wasMaterialsPopupOpen == false)
                {
                    Logger?.LogDebug("材料选择弹窗选中项变化，但弹窗当前未打开，可能是外部修改了 SelectedItem，忽略本次变化");
                    return;
                }

                var selectedId = selectedItem.Value.Id;
                var selectedMaterial = selectedItem.Value;

                SelectedSolidWasteMaterial = selectedMaterial;
                if (!SolidWasteMaterials.Any(m => m.Id == selectedId))
                    SolidWasteMaterials.Insert(0, selectedMaterial);

                IsMaterialsPopupOpen = false;

                Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        SolidWasteMaterials.Clear();
                        var materials = await _materialService.GetAllMaterialsAsync();
                        foreach (var material in materials)
                            SolidWasteMaterials.Add(material);
                        SelectedSolidWasteMaterial = SolidWasteMaterials.FirstOrDefault(m => m.Id == selectedId);
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "同步材料选择失败");
                    }
                });
            });

        // 供应商：服务端分页（支持按搜索新增）
        ProvidersPopupViewModel = new GenericSelectionPopupViewModel<ProviderDto>(
            pagingMode: GenericSelectionPagingMode.ServerSide,
            displayTextSelector: p => p.ProviderName,
            logger: Logger,
            loadPageFunc: (search, pageIndex, pageSize, selectedIds) =>
                _providerService.GetPagedProvidersAsync(search, pageIndex, pageSize, selectedIds),
            getSelectedId: p => p.Id,
            createNewItemFunc: async name =>
            {
                var deliveryType = _listItem.DeliveryType ?? DeliveryType.Receiving;
                var created = await _providerService.CreateProviderAsync(name, deliveryType);
                return (ProviderDto?)new ProviderDto
                {
                    Id = created.Id,
                    ProviderType = created.ProviderType ?? (int)deliveryType,
                    ProviderName = created.ProviderName,
                    ContactName = created.ContectName,
                    ContactPhone = created.ContectPhone
                };
            },
            confirmNewNameFunc: async proposed =>
                await ConfirmTextInteraction.Handle(new ConfirmTextRequest(
                        Title: "确认新增供应商",
                        Message: "将新增一条供应商，请确认名称：",
                        InitialValue: proposed)));

        _ = ProvidersPopupViewModel.InitializeAsync();

        var wasProvidersPopupOpen = false;
        this.WhenAnyValue(x => x.IsProvidersPopupOpen, x => x.ProvidersPopupViewModel.SelectedItem)
            .Subscribe(tuple =>
            {
                var (isOpen, selectedItem) = tuple;
                if (ProvidersPopupViewModel == null) return;

                // 1) 先处理"弹窗刚打开"
                if (isOpen && !wasProvidersPopupOpen)
                {
                    wasProvidersPopupOpen = true;
                    ProvidersPopupViewModel.SearchText = string.Empty;
                    ProvidersPopupViewModel.CurrentPage = 1;
                    ProvidersPopupViewModel.PendingSelectedIds = SelectedProvider != null
                        ? new System.Collections.Generic.List<int> { SelectedProvider.Id }
                        : null;
                    if (SelectedProvider != null)
                    {
                        ProvidersPopupViewModel.SelectedItem = new GenericSelectionItem<ProviderDto>
                        {
                            Value = SelectedProvider,
                            DisplayText = SelectedProvider.ProviderName
                        };
                    }
                    else
                    {
                        ProvidersPopupViewModel.SelectedItem = null;
                    }
                }

                if (!isOpen)
                {
                    wasProvidersPopupOpen = false;
                    return;
                }

                // 2) 再处理"选中项与当前不同 → 回写并关弹窗"
                if (selectedItem == null)
                {
                    Logger?.LogDebug("供应商选择弹窗选中项为 null，忽略本次变化");
                    return;
                }
                if (wasProvidersPopupOpen == false)
                {
                    Logger?.LogDebug("供应商选择弹窗选中项变化，但弹窗当前未打开，可能是外部修改了 SelectedItem，忽略本次变化");
                    return;
                }

                var selectedId = selectedItem.Value.Id;
                var selectedProvider = selectedItem.Value;

                SelectedProvider = selectedProvider;
                SelectedProviderId = selectedId;
                if (!Providers.Any(p => p.Id == selectedId))
                    Providers.Insert(0, selectedProvider);

                IsProvidersPopupOpen = false;

                Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        await LoadProvidersAsync();
                        SelectedProvider = Providers.FirstOrDefault(p => p.Id == selectedId);
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "同步供应商选择失败");
                    }
                });
            });
    }

    protected override async Task LoadDropdownDataAsync()
    {
        try
        {
            await Task.WhenAll(
                LoadProvidersAsync(),
                LoadConfigurationDataAsync()
            );

            if (SelectedProviderId.HasValue)
            {
                var provider = Providers.FirstOrDefault(p => p.Id == SelectedProviderId.Value);
                if (provider != null)
                {
                    SelectedProvider = provider;
                    ProvidersPopupViewModel.SelectedItem = new GenericSelectionItem<ProviderDto>
                    {
                        Value = provider,
                        DisplayText = provider.ProviderName
                    };
                }
            }

            // 加载固废模式数据
            await LoadSolidWasteDataAsync();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载下拉列表数据失败");
        }
    }

    private async Task LoadProvidersAsync()
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

    private async Task LoadConfigurationDataAsync()
    {
        try
        {
            // 加载街道配置
            Streets.Clear();
            var streets = _streetsConfig.Value.Streets ?? Array.Empty<string>();
            foreach (var street in streets.OrderBy(s => s))
                Streets.Add(street);

            // 加载固废类型配置
            SolidWasteTypes.Clear();
            var solidWasteTypes = _solidWasteTypeConfig.Value.SolidWasteTypes ?? Array.Empty<string>();
            foreach (var type in solidWasteTypes.OrderBy(t => t))
                SolidWasteTypes.Add(type);

            // 加载材料列表到 SolidWasteMaterials（用于固废模式材料选择）
            SolidWasteMaterials.Clear();
            var materials = await _materialService.GetAllMaterialsAsync();
            foreach (var material in materials)
                SolidWasteMaterials.Add(material);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载配置数据失败");
        }
    }

    private async Task LoadSolidWasteDataAsync()
    {
        try
        {
            if (_listItem.ItemType == WeighingListItemType.WeighingRecord)
            {
                var record = await _weighingRecordRepository.GetAsync(_listItem.Id);

                // 从 ExtraProperties 读取 SolidWaste 数据
                SolidWasteOrderNumber = record.GetSolidWasteOrderNumber();
                SelectedStreet = record.GetStreet();
                if (!string.IsNullOrEmpty(SelectedStreet))
                {
                    StreetsPopupViewModel.SelectedItem = new GenericSelectionItem<string>
                    {
                        Value = SelectedStreet,
                        DisplayText = SelectedStreet
                    };
                }
                SelectedSolidWasteType = record.GetSolidWasteType();
                SelectedProviderId = record.ProviderId;

                // 读取 MaterialId 和 WaybillQuantity
                var materialId = record.GetProperty<int?>("SolidWasteInfo.MaterialId");

                if (materialId.HasValue)
                {
                    var material = SolidWasteMaterials.FirstOrDefault(m => m.Id == materialId.Value);
                    if (material != null)
                    {
                        SelectedSolidWasteMaterial = material;
                        MaterialsPopupViewModel.SelectedItem = new GenericSelectionItem<Material>
                        {
                            Value = material,
                            DisplayText = material.Name ?? string.Empty
                        };

                        // 加载单位并自动选择第一个
                        var units = await LoadMaterialUnitsForRowAsync(material.Id);
                        if (MaterialItems.Count > 0)
                        {
                            var firstRow = MaterialItems[0];
                            firstRow.SetMaterialUnits(units);
                            if (units.Count > 0)
                            {
                                firstRow.InitializeSelection(material, units, units[0].Id);
                            }
                        }
                    }
                }
            }
            else if (_listItem.ItemType == WeighingListItemType.Waybill)
            {
                var waybillRepository = _serviceProvider.GetRequiredService<IRepository<Waybill, long>>();
                var waybill = await waybillRepository.GetAsync(_listItem.Id);

                // 从 ExtraProperties 读取 SolidWaste 数据
                SolidWasteOrderNumber = waybill.GetSolidWasteOrderNumber();
                SelectedStreet = waybill.GetStreet();
                if (!string.IsNullOrEmpty(SelectedStreet))
                {
                    StreetsPopupViewModel.SelectedItem = new GenericSelectionItem<string>
                    {
                        Value = SelectedStreet,
                        DisplayText = SelectedStreet
                    };
                }
                SelectedSolidWasteType = waybill.GetSolidWasteType();
                SelectedProviderId = waybill.ProviderId;

                // 读取 MaterialId
                var materialId = waybill.GetProperty<int?>("SolidWasteInfo.MaterialId");

                // 如果 ExtraProperties 中没有，尝试从标准字段读取
                if (!materialId.HasValue && waybill.MaterialId.HasValue)
                {
                    materialId = waybill.MaterialId;
                }

                if (materialId.HasValue)
                {
                    var material = SolidWasteMaterials.FirstOrDefault(m => m.Id == materialId.Value);
                    if (material != null)
                    {
                        SelectedSolidWasteMaterial = material;
                        MaterialsPopupViewModel.SelectedItem = new GenericSelectionItem<Material>
                        {
                            Value = material,
                            DisplayText = material.Name ?? string.Empty
                        };

                        // 加载单位并自动选择第一个
                        var units = await LoadMaterialUnitsForRowAsync(material.Id);
                        if (MaterialItems.Count > 0)
                        {
                            var firstRow = MaterialItems[0];
                            firstRow.SetMaterialUnits(units);

                            var unitId = waybill.MaterialUnitId ?? units.FirstOrDefault()?.Id;
                            if (unitId.HasValue)
                            {
                                firstRow.InitializeSelection(material, units, unitId.Value);
                            }
                            else if (units.Count > 0)
                            {
                                firstRow.InitializeSelection(material, units, units[0].Id);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载固废模式数据失败");
        }
    }

    protected override async Task<ObservableCollection<MaterialUnitDto>> LoadMaterialUnitsForRowAsync(int materialId)
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

    #region 抽象方法实现

    protected override async Task SaveCoreAsync()
    {
        var providerId = SelectedProviderId;
        var materialId = SelectedSolidWasteMaterial?.Id;
        var materialUnitId = MaterialItems.FirstOrDefault()?.SelectedMaterialUnit?.Id;
        var weighingMatchingService = _serviceProvider.GetRequiredService<IWeighingMatchingService>();

        await weighingMatchingService.UpdateSolidWasteModeAsync(new UpdateSolidWasteModeInput(
            _listItem.Id,
            _listItem.ItemType,
            PlateNumber,
            providerId,
            materialId,
            materialUnitId,
            SelectedSolidWasteType,
            SelectedStreet,
            SolidWasteOrderNumber,
            Remark,
            null,
            IsWeighingRecord ? SelectedDeliveryType : null));
    }

    protected override async Task CompleteCoreAsync()
    {
        var providerId = SelectedProviderId;
        var materialId = SelectedSolidWasteMaterial?.Id;
        var materialUnitId = MaterialItems.FirstOrDefault()?.SelectedMaterialUnit?.Id;

        if (providerId == null)
        {
            await ShowMessageBoxAsync("请先选择供应商");
            throw new InvalidOperationException("请先选择供应商");
        }

        if (materialId == null)
        {
            await ShowMessageBoxAsync("请先选择材料");
            throw new InvalidOperationException("请先选择材料");
        }

        if (string.IsNullOrEmpty(SelectedStreet))
        {
            await ShowMessageBoxAsync("请先选择镇街");
            throw new InvalidOperationException("请先选择镇街");
        }

        if (string.IsNullOrEmpty(SelectedSolidWasteType))
        {
            await ShowMessageBoxAsync("请先选择类型");
            throw new InvalidOperationException("请先选择类型");
        }

        if (string.IsNullOrEmpty(SolidWasteOrderNumber))
        {
            await ShowMessageBoxAsync("请填写联单号");
            throw new InvalidOperationException("请填写联单号");
        }

        var weighingMatchingService = _serviceProvider.GetRequiredService<IWeighingMatchingService>();

        // 先保存固废模式数据
        await weighingMatchingService.UpdateSolidWasteModeAsync(new UpdateSolidWasteModeInput(
            _listItem.Id,
            _listItem.ItemType,
            PlateNumber,
            providerId,
            materialId,
            materialUnitId,
            SelectedSolidWasteType,
            SelectedStreet,
            SolidWasteOrderNumber,
            Remark,
            null,
            IsWeighingRecord ? SelectedDeliveryType : null));

        // 然后完成订单
        await weighingMatchingService.CompleteOrderAsync(_listItem.Id);
    }

    #endregion
}
