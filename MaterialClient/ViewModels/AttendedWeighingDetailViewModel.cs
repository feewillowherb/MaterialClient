using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Models;
using MaterialClient.Common.Providers;
using MaterialClient.Common.Services;
using Volo.Abp;
using Volo.Abp.Data;
using MaterialClient.Views;
using MaterialClient.Views.AttendedWeighing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private WeighingListItemDto _listItem = null!;
    private readonly IMaterialService _materialService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IOptions<StreetsConfig> _streetsConfig;
    private readonly IOptions<SolidWasteTypeConfig> _solidWasteTypeConfig;
    private readonly ISettingsService _settingsService;

    public AttendedWeighingDetailViewModel(
        IServiceProvider serviceProvider)
        : base(serviceProvider.GetService<ILogger<AttendedWeighingDetailViewModel>>())
    {
        _serviceProvider = serviceProvider;
        _weighingRecordRepository = _serviceProvider.GetRequiredService<IRepository<WeighingRecord, long>>();
        _materialService = _serviceProvider.GetRequiredService<IMaterialService>();
        _streetsConfig = _serviceProvider.GetRequiredService<IOptions<StreetsConfig>>();
        _solidWasteTypeConfig = _serviceProvider.GetRequiredService<IOptions<SolidWasteTypeConfig>>();
        _settingsService = _serviceProvider.GetRequiredService<ISettingsService>();

        // 初始化材料选择弹窗 ViewModel
        InitializeMaterialsSelectionPopup();

        // 初始化 SolidWaste 下拉选择弹窗（镇街/材料/供应商）
        InitializeSolidWasteSelectionPopups();

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

        // 订阅 WeighingMode 变化，更新 IsSolidWasteMode
        this.WhenAnyValue(x => x.WeighingMode)
            .Subscribe(mode => IsSolidWasteMode = mode == WeighingMode.SolidWaste);

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
                            // 自动选择第一个单位（按 UnitName 排序后的第一个）
                            // 注意：LoadMaterialUnitsForRowAsync 已经按 UnitName 排序
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

    [Reactive] private MaterialsSelectionPopupViewModel? _materialsSelectionPopupViewModel;

    private IDisposable? _materialSelectionSubscription;

    /// <summary>
    ///     临时保存的拍照文件路径（从父 ViewModel 传递）
    /// </summary>
    private string? _capturedBillPhotoPath;

    // SolidWaste 模式相关属性
    [Reactive] private WeighingMode _weighingMode = WeighingMode.Standard;

    [Reactive] private bool _isSolidWasteMode;

    [Reactive] private string? _solidWasteOrderNumber;

    [Reactive] private ObservableCollection<string> _streets = new();

    [Reactive] private string? _selectedStreet;

    [Reactive] private ObservableCollection<string> _solidWasteTypes = new();

    [Reactive] private string? _selectedSolidWasteType;

    [Reactive] private ObservableCollection<Material> _solidWasteMaterials = new();

    [Reactive] private Material? _selectedSolidWasteMaterial;

    // SolidWaste 模式：增强下拉选择弹窗（搜索/分页）
    [Reactive] private GenericSelectionPopupViewModel<string>? _streetsPopupViewModel;
    [Reactive] private GenericSelectionPopupViewModel<Material>? _materialsPopupViewModel;
    [Reactive] private GenericSelectionPopupViewModel<ProviderDto>? _providersPopupViewModel;

    [Reactive] private bool _isStreetsPopupOpen;
    [Reactive] private bool _isMaterialsPopupOpen;
    [Reactive] private bool _isProvidersPopupOpen;

    /// <summary>
    ///     供应商标签文本（根据当前记录的收发料类型动态显示）
    /// </summary>
    public string ProviderLabelText
    {
        get
        {
            // 收料时显示"发货单位"，发料时显示"收货单位"
            return _listItem?.DeliveryType == DeliveryType.Receiving
                ? "发货单位"
                : "收货单位";
        }
    }

    public string DeliveryTypeTitleText
    {
        get
        {
            return _listItem?.DeliveryType switch
            {
                DeliveryType.Sending => "发料信息",
                DeliveryType.Receiving => "收料信息",
                _ => "物料信息"
            };
        }
    }

    /// <summary>
    ///     完成按钮文本（根据当前记录的收发料类型动态显示）
    /// </summary>
    public string CompleteButtonText
    {
        get
        {
            var deliveryType = _listItem?.DeliveryType ?? DeliveryType.Receiving;
            return deliveryType == DeliveryType.Sending ? "完成本次发货" : "完成本次收货";
        }
    }

    #endregion

    #region 初始化

    private void InitializeMaterialsSelectionPopup()
    {
        // 直接创建材料选择弹窗 ViewModel 实例，而不是通过 IOC 容器获取
        // 这样可以确保使用的是同一个实例，避免数据传递问题
        MaterialsSelectionPopupViewModel = new MaterialsSelectionPopupViewModel(_serviceProvider);

        // 订阅材料选择事件（使用 Where 过滤 null，确保只有选择时才触发）
        // 保存订阅以防止被垃圾回收
        _materialSelectionSubscription = MaterialsSelectionPopupViewModel.WhenAnyValue(x => x.SelectedMaterial)
            .Where(material => material != null)
            .Subscribe(material =>
            {
                if (material != null)
                {
                    // 使用 Post 延迟执行，确保在下一个消息循环执行，避免在属性变化通知中间执行
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            SelectMaterialCommand.Execute(material);
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogError(ex, "执行材料选择命令时发生错误");
                        }
                    });
                }
            });

        // 当弹窗打开时刷新数据并清空选择
        this.WhenAnyValue(x => x.IsMaterialPopupOpen)
            .Subscribe(isOpen =>
            {
                if (isOpen && MaterialsSelectionPopupViewModel != null)
                {
                    // 清空之前的选择，确保每次打开弹窗时都是干净的状态
                    MaterialsSelectionPopupViewModel.SelectedMaterial = null;
                    _ = MaterialsSelectionPopupViewModel.RefreshAsync();
                }
            });
    }

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
                    
                    // Sync current selection to popup
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
            loadPageFunc: (search, pageIndex, pageSize) =>
                _materialService.GetPagedMaterialsAsync(search, pageIndex, pageSize),
            createNewItemFunc: async name =>
                (Material?)await _materialService.CreateMaterialAsync(name));

        _ = MaterialsPopupViewModel.InitializeAsync();

        MaterialsPopupViewModel.WhenAnyValue(x => x.SelectedItem)
            .Where(item => item != null)
            .Subscribe(item =>
            {
                if (item == null) return;
                SelectedSolidWasteMaterial = item.Value;
                IsMaterialsPopupOpen = false;
            });

        this.WhenAnyValue(x => x.IsMaterialsPopupOpen)
            .Subscribe(isOpen =>
            {
                if (isOpen && MaterialsPopupViewModel != null)
                {
                    MaterialsPopupViewModel.SearchText = string.Empty;
                    MaterialsPopupViewModel.CurrentPage = 1;
                    
                    // Sync current selection to popup
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
                    
                    _ = MaterialsPopupViewModel.RefreshAsync();
                }
            });

        // 供应商：服务端分页（支持按搜索新增）
        ProvidersPopupViewModel = new GenericSelectionPopupViewModel<ProviderDto>(
            pagingMode: GenericSelectionPagingMode.ServerSide,
            displayTextSelector: p => p.ProviderName,
            logger: Logger,
            loadPageFunc: (search, pageIndex, pageSize) =>
                _materialService.GetPagedProvidersAsync(search, pageIndex, pageSize),
            createNewItemFunc: async name =>
            {
                var deliveryType = _listItem.DeliveryType ?? DeliveryType.Receiving;
                var created = await _materialService.CreateProviderAsync(name, deliveryType);
                return (ProviderDto?)new ProviderDto
                {
                    Id = created.Id,
                    ProviderType = created.ProviderType ?? (int)deliveryType,
                    ProviderName = created.ProviderName,
                    ContactName = created.ContectName,
                    ContactPhone = created.ContectPhone
                };
            });

        _ = ProvidersPopupViewModel.InitializeAsync();

        ProvidersPopupViewModel.WhenAnyValue(x => x.SelectedItem)
            .Where(item => item != null)
            .Subscribe(item =>
            {
                if (item == null) return;
                var selectedId = item.Value.Id;
                
                // Close popup first
                IsProvidersPopupOpen = false;
                
                // Reload Providers and sync selection
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

        this.WhenAnyValue(x => x.IsProvidersPopupOpen)
            .Subscribe(isOpen =>
            {
                if (isOpen && ProvidersPopupViewModel != null)
                {
                    ProvidersPopupViewModel.SearchText = string.Empty;
                    ProvidersPopupViewModel.CurrentPage = 1;
                    
                    // Sync current selection to popup
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
                    
                    _ = ProvidersPopupViewModel.RefreshAsync();
                }
            });
    }

    public void InitializeData(WeighingListItemDto listItem, string? capturedBillPhotoPath = null)
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

        // 初始化 WeighingMode：使用记录的实际模式
        WeighingMode = _listItem.WeighingMode;

        // 通知 ProviderLabelText 属性变化（因为它依赖于 _listItem.DeliveryType）
        this.RaisePropertyChanged(nameof(ProviderLabelText));

        // 通知 DeliveryTypeTitleText 属性变化（因为它依赖于 _listItem.DeliveryType）
        this.RaisePropertyChanged(nameof(DeliveryTypeTitleText));

        // 通知 CompleteButtonText 属性变化（因为它依赖于 _listItem.DeliveryType）
        this.RaisePropertyChanged(nameof(CompleteButtonText));

        // 保存临时拍照文件路径
        _capturedBillPhotoPath = capturedBillPhotoPath;

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
                LoadMaterialsAsync(),
                LoadConfigurationDataAsync()
            );

            // 检查是否需要获取推荐数据
            // 检查第一个物料是否有 MaterialId 和 MaterialUnitId，以及是否有 ProviderId
            var firstMaterialDto = _listItem.Materials.Count > 0 ? _listItem.Materials[0] : null;
            var hasMaterialId = firstMaterialDto?.MaterialId.HasValue ?? _listItem.MaterialId.HasValue;
            var hasMaterialUnitId = firstMaterialDto?.MaterialUnitId.HasValue ?? _listItem.MaterialUnitId.HasValue;
            var hasProviderId = _listItem.ProviderId.HasValue;

            var needsRecommendation = (!hasMaterialId || !hasMaterialUnitId || !hasProviderId) &&
                                      !string.IsNullOrWhiteSpace(PlateNumber);

            WaybillRecommendationDto? recommendation = null;
            if (needsRecommendation)
            {
                try
                {
                    var weighingMatchingService = _serviceProvider.GetRequiredService<IWeighingMatchingService>();
                    recommendation = await weighingMatchingService.GetRecommendationByPlateNumberAsync(PlateNumber);

                    if (recommendation != null)
                    {
                        Logger?.LogInformation(
                            "获取到推荐数据: MaterialId={MaterialId}, ProviderId={ProviderId}, MaterialUnitId={MaterialUnitId}",
                            recommendation.MaterialId, recommendation.ProviderId, recommendation.MaterialUnitId);
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "获取推荐数据失败");
                }
            }

            // 应用推荐数据填充缺失的字段
            if (recommendation != null)
            {
                // 填充 ProviderId
                if (!hasProviderId && recommendation.ProviderId.HasValue)
                {
                    SelectedProviderId = recommendation.ProviderId.Value;
                    _listItem.ProviderId = recommendation.ProviderId.Value;
                }

                // 填充 MaterialId 和 MaterialUnitId（仅当第一行没有数据时）
                if (MaterialItems.Count > 0)
                {
                    // 如果第一行没有 MaterialId，使用推荐数据
                    if (!hasMaterialId && recommendation.MaterialId.HasValue)
                    {
                        // 更新 _listItem 的 Materials（用于后续处理）
                        if (_listItem.Materials.Count == 0)
                        {
                            _listItem.Materials.Add(new WeighingListItemMaterialDto
                            {
                                MaterialId = recommendation.MaterialId,
                                MaterialUnitId = recommendation.MaterialUnitId,
                                WaybillQuantity = recommendation.WaybillQuantity ?? _listItem.WaybillQuantity
                            });
                        }
                        else
                        {
                            var materialDto = _listItem.Materials[0];
                            if (!materialDto.MaterialId.HasValue)
                                materialDto.MaterialId = recommendation.MaterialId;
                            if (!materialDto.MaterialUnitId.HasValue && recommendation.MaterialUnitId.HasValue)
                                materialDto.MaterialUnitId = recommendation.MaterialUnitId;
                            // not fill WaybillQuantity here anymore
                            // if (!materialDto.WaybillQuantity.HasValue && recommendation.WaybillQuantity.HasValue)
                            //     materialDto.WaybillQuantity = recommendation.WaybillQuantity;
                        }
                    }
                    // 如果只有 MaterialUnitId 缺失，也填充
                    else if (hasMaterialId && !hasMaterialUnitId && recommendation.MaterialUnitId.HasValue)
                    {
                        if (_listItem.Materials.Count > 0)
                        {
                            _listItem.Materials[0].MaterialUnitId = recommendation.MaterialUnitId;
                        }
                    }
                }
            }

            if (SelectedProviderId.HasValue)
                SelectedProvider = Providers.FirstOrDefault(p => p.Id == SelectedProviderId.Value);

            // 如果是 SolidWaste 模式，加载 SolidWaste 数据
            if (IsSolidWasteMode)
            {
                await LoadSolidWasteDataAsync();
            }
            else
            {
                // Standard 模式：根据 _listItem.Materials 初始化每个 MaterialItemRow
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
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载下拉列表数据失败");
            // 如果加载失败，保持当前状态
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
                SelectedSolidWasteType = record.GetSolidWasteType();
                SelectedProviderId = record.ProviderId;
                _listItem.ProviderId = record.ProviderId;

                // 读取 MaterialId 和 WaybillQuantity
                var materialId = record.GetProperty<int?>("SolidWasteInfo.MaterialId");
                var waybillQuantity = record.GetProperty<decimal?>("SolidWasteInfo.WaybillQuantity");

                if (materialId.HasValue)
                {
                    var material = SolidWasteMaterials.FirstOrDefault(m => m.Id == materialId.Value);
                    if (material != null)
                    {
                        SelectedSolidWasteMaterial = material;

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
                SelectedSolidWasteType = waybill.GetSolidWasteType();
                SelectedProviderId = waybill.ProviderId;
                _listItem.ProviderId = waybill.ProviderId;

                // 读取 MaterialId 和 WaybillQuantity
                var materialId = waybill.GetProperty<int?>("SolidWasteInfo.MaterialId");
                var waybillQuantity = waybill.GetProperty<decimal?>("SolidWasteInfo.WaybillQuantity");

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

                        // 加载单位并自动选择第一个
                        var units = await LoadMaterialUnitsForRowAsync(material.Id);
                        if (MaterialItems.Count > 0)
                        {
                            var firstRow = MaterialItems[0];
                            firstRow.SetMaterialUnits(units);

                            // 如果 Waybill 有 MaterialUnitId，使用它；否则选择第一个
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


    private async Task LoadProvidersAsync()
    {
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
            Logger?.LogError(ex, "加载供应商列表失败");
            // 如果加载失败，保持空列表
        }
    }

    private async Task LoadMaterialsAsync()
    {
        try
        {
            var materials = await _materialService.GetAllMaterialsAsync();
            Materials.Clear();
            foreach (var material in materials) Materials.Add(material);
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
            // 如果加载失败，返回空列表
        }

        return result;
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
            // 如果加载失败，保持空列表
        }

        await Task.CompletedTask;
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

            if (IsSolidWasteMode)
            {
                // SolidWaste 模式：直接更新实体并保存到 ExtraProperties
                await SaveSolidWasteModeAsync();
            }
            else
            {
                // Standard 模式：使用现有的 UpdateListItemAsync
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
            }

            // 检查是否有临时保存的BillPhoto文件，如果有则创建附件
            if (!string.IsNullOrEmpty(_capturedBillPhotoPath))
            {
                var billPhotoPath = _capturedBillPhotoPath;

                // 检查文件是否存在
                if (File.Exists(billPhotoPath))
                {
                    var attachmentService = _serviceProvider.GetRequiredService<IAttachmentService>();
                    await attachmentService.CreateOrReplaceBillPhotoAsync(_listItem, billPhotoPath);

                    // 清空临时文件路径
                    _capturedBillPhotoPath = null;
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

    private async Task SaveSolidWasteModeAsync()
    {
        var providerId = SelectedProviderId;
        var materialId = SelectedSolidWasteMaterial?.Id;
        var materialUnitId = MaterialItems.FirstOrDefault()?.SelectedMaterialUnit?.Id;
        var weighingMatchingService = _serviceProvider.GetRequiredService<IWeighingMatchingService>();
        try
        {
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
                null));
        }
        catch (BusinessException ex)
        {
            await ShowMessageBoxAsync(ex.Message);
        }
        catch (ArgumentException ex)
        {
            await ShowMessageBoxAsync(ex.Message);
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
                // 触发 ManualMatchSaveCompleted 事件（如果有 WaybillId）
                if (matchWindow.SavedWaybillId.HasValue)
                {
                    ManualMatchSaveCompleted?.Invoke(this, new ManualMatchSaveCompletedEventArgs
                    {
                        WaybillId = matchWindow.SavedWaybillId
                    });
                }

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

    /// <summary>
    /// 异步显示消息框，不阻塞命令执行，用于验证失败时解除按钮锁定状态
    /// </summary>
    private void ShowMessageBoxAsyncWithoutBlocking(string message)
    {
        Dispatcher.UIThread.Post(async () =>
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
        }, DispatcherPriority.Normal);
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
            // 验证车牌号格式
            if (!PlateNumberValidator.IsValidChinesePlateNumber(PlateNumber))
            {
                ShowMessageBoxAsyncWithoutBlocking("车牌号不符合规范请修改");
                return;
            }

            if (IsSolidWasteMode)
            {
                // SolidWaste 模式：保存并完成
                await CompleteSolidWasteModeAsync();
            }
            else
            {
                // Standard 模式：验证并保存，然后完成
                await CompleteStandardModeAsync();
            }

            // 检查是否有临时保存的BillPhoto文件，如果有则创建附件
            if (!string.IsNullOrEmpty(_capturedBillPhotoPath))
            {
                var billPhotoPath = _capturedBillPhotoPath;

                // 检查文件是否存在
                if (File.Exists(billPhotoPath))
                {
                    var attachmentService = _serviceProvider.GetRequiredService<IAttachmentService>();
                    await attachmentService.CreateOrReplaceBillPhotoAsync(_listItem, billPhotoPath);

                    // 清空临时文件路径
                    _capturedBillPhotoPath = null;
                }
            }

            CompleteCompleted?.Invoke(this, new CompleteCompletedEventArgs
            {
                Id = _listItem.Id,
                OrderType = _listItem.OrderType
            });
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "完成本次收货失败");
        }
    }

    private async Task CompleteStandardModeAsync()
    {
        // 验证是否选择了供应商
        if (SelectedProvider == null)
        {
            ShowMessageBoxAsyncWithoutBlocking("请选择供应商");
            throw new InvalidOperationException("请选择供应商");
        }

        var firstRow = MaterialItems.FirstOrDefault();
        var materialId = firstRow?.SelectedMaterial?.Id;
        var materialUnitId = firstRow?.SelectedMaterialUnit?.Id;
        var providerId = SelectedProvider?.Id;
        var waybillQuantity = firstRow?.WaybillQuantity;

        // 验证 materialId、materialUnitId、waybillQuantity 都不能为空
        if (!materialId.HasValue)
        {
            ShowMessageBoxAsyncWithoutBlocking("请选择物料");
            throw new InvalidOperationException("请选择物料");
        }

        if (!materialUnitId.HasValue)
        {
            ShowMessageBoxAsyncWithoutBlocking("请选择物料单位");
            throw new InvalidOperationException("请选择物料单位");
        }

        if (!waybillQuantity.HasValue)
        {
            ShowMessageBoxAsyncWithoutBlocking("请输入运单数量");
            throw new InvalidOperationException("请输入运单数量");
        }

        var weighingMatchingService = _serviceProvider.GetRequiredService<IWeighingMatchingService>();

        // 先更新数据
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

        // 然后完成订单
        await weighingMatchingService.CompleteOrderAsync(_listItem.Id);
    }

    private async Task CompleteSolidWasteModeAsync()
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

        try
        {
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
                null));

            // 然后完成订单
            await weighingMatchingService.CompleteOrderAsync(_listItem.Id);
        }
        catch (BusinessException ex)
        {
            ShowMessageBoxAsyncWithoutBlocking(ex.Message);
            throw;
        }
        catch (ArgumentException ex)
        {
            ShowMessageBoxAsyncWithoutBlocking(ex.Message);
            throw;
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

        // 清空弹窗的 SelectedMaterial，以便下次选择时能再次触发事件
        if (MaterialsSelectionPopupViewModel != null)
        {
            MaterialsSelectionPopupViewModel.SelectedMaterial = null;
        }

        return Task.CompletedTask;
    }

    #endregion


    #region 事件

    public event EventHandler? SaveCompleted;
    public event EventHandler? AbolishCompleted;
    public event EventHandler? CloseRequested;
    public event EventHandler? MatchCompleted;
    public event EventHandler<CompleteCompletedEventArgs>? CompleteCompleted;
    public event EventHandler<ManualMatchSaveCompletedEventArgs>? ManualMatchSaveCompleted;

    #endregion
}

public class CompleteCompletedEventArgs : EventArgs
{
    public long? Id { get; init; }
    public OrderTypeEnum? OrderType { get; init; }
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

                // 如果单位列表不为空且当前没有选中的单位，自动选择第一个
                if (MaterialUnits.Count > 0 && SelectedMaterialUnit == null)
                {
                    SelectedMaterialUnit = MaterialUnits[0];
                }
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