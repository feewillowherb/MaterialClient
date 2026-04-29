using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using MaterialClient.Common.Providers;
using MaterialClient.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MaterialClient.ViewModels;

/// <summary>
///     SolidWaste mode weighing detail view model.
///     Handles solid waste specific data like streets, waste types, order numbers,
///     and uses paginated selector components for provider/material/street selection.
/// </summary>
public partial class SolidWasteWeighingDetailViewModel : AttendedWeighingDetailViewModelBase, ITransientDependency
{
    private readonly IOptions<StreetsConfig> _streetsConfig;
    private readonly IOptions<SolidWasteTypeConfig> _solidWasteTypeConfig;
    private readonly IRecommendationService _recommendationService;
    private readonly ISettingsService _settingsService;

    [Reactive] private string? _solidWasteOrderNumber;
    [Reactive] private ObservableCollection<string> _streets = new();
    [Reactive] private string? _selectedStreet;
    [Reactive] private ObservableCollection<string> _solidWasteTypes = new();
    [Reactive] private string? _selectedSolidWasteType;
    [Reactive] private ObservableCollection<Material> _solidWasteMaterials = new();
    [Reactive] private Material? _selectedSolidWasteMaterial;
    [Reactive] private SelectionItem? _selectedProviderItem;
    [Reactive] private SelectionItem? _selectedMaterialItem;
    [Reactive] private SelectionItem? _selectedStreetItem;

    public Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<SelectionItem>>> ProviderLoadPageAsync { get; }
    public Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<SelectionItem>>> MaterialLoadPageAsync { get; }
    public Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<SelectionItem>>> StreetLoadPageAsync { get; }
    public Func<string, Task<SelectionItem?>>? ProviderCreateNewAsync { get; }
    public Func<string, Task<SelectionItem?>>? MaterialCreateNewAsync { get; }

    public override bool IsSolidWasteMode => true;

    public SolidWasteWeighingDetailViewModel(
        IServiceProvider serviceProvider,
        IRecommendationService recommendationService,
        ISettingsService settingsService)
        : base(serviceProvider, serviceProvider.GetService<ILogger<SolidWasteWeighingDetailViewModel>>())
    {
        _streetsConfig = _serviceProvider.GetRequiredService<IOptions<StreetsConfig>>();
        _solidWasteTypeConfig = _serviceProvider.GetRequiredService<IOptions<SolidWasteTypeConfig>>();
        _recommendationService = recommendationService;
        _settingsService = settingsService;

        ProviderLoadPageAsync = LoadProvidersPageAsync;
        MaterialLoadPageAsync = LoadMaterialsPageAsync;
        StreetLoadPageAsync = LoadStreetsPageAsync;
        ProviderCreateNewAsync = CreateNewProviderAsync;
        MaterialCreateNewAsync = CreateNewMaterialAsync;

        this.WhenAnyValue(x => x.SelectedProviderItem)
            .Subscribe(item =>
            {
                if (item != null)
                {
                    SelectedProviderId = item.Id;
                }
            });

        this.WhenAnyValue(x => x.SelectedMaterialItem)
            .Where(m => m != null)
            .Subscribe(async item =>
            {
                if (item == null) return;
                var materials = await _materialService.GetAllMaterialsAsync();
                var material = materials.FirstOrDefault(m => m.Id == item.Id);
                if (material != null) SelectedSolidWasteMaterial = material;
            });

        this.WhenAnyValue(x => x.SelectedStreetItem)
            .Subscribe(item =>
            {
                SelectedStreet = item?.Name;
            });

        // SolidWaste mode: auto-select first unit when material is selected
        this.WhenAnyValue(x => x.SelectedSolidWasteMaterial)
            .Where(material => material != null)
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

        // SolidWaste mode: waybill quantity = actual weight (GoodsWeight)
        this.WhenAnyValue(x => x.GoodsWeight)
            .Subscribe(_ =>
            {
                if (MaterialItems.Count > 0)
                {
                    var firstRow = MaterialItems[0];
                    firstRow.WaybillQuantity = GoodsWeight;
                }
            });
    }

    protected override async Task LoadModeSpecificDataAsync()
    {
        // Load SolidWaste-specific configuration data
        await LoadConfigurationDataAsync();

        // Load SolidWaste data from ExtraProperties
        await LoadSolidWasteDataAsync();

        // Apply recommendation data to fill missing fields
        await ApplyRecommendationAsync();
    }

    private async Task LoadConfigurationDataAsync()
    {
        try
        {
            // Load streets configuration
            Streets.Clear();
            var streets = _streetsConfig.Value.Streets ?? Array.Empty<string>();
            foreach (var street in streets.OrderBy(s => s))
                Streets.Add(street);

            // Load solid waste types configuration
            SolidWasteTypes.Clear();
            var solidWasteTypes = _solidWasteTypeConfig.Value.SolidWasteTypes ?? Array.Empty<string>();
            foreach (var type in solidWasteTypes.OrderBy(t => t))
                SolidWasteTypes.Add(type);

            // Load materials list into SolidWasteMaterials (for solid waste mode material selection)
            SolidWasteMaterials.Clear();
            var materials = await _materialService.GetAllMaterialsAsync();
            foreach (var material in materials)
                SolidWasteMaterials.Add(material);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载配置数据失败");
        }

        await Task.CompletedTask;
    }

    private async Task LoadSolidWasteDataAsync()
    {
        try
        {
            if (_listItem.ItemType == WeighingListItemType.WeighingRecord)
            {
                var record = await _serviceProvider.GetRequiredService<IRepository<WeighingRecord, long>>().GetAsync(_listItem.Id);

                // Read SolidWaste data from ExtraProperties
                SolidWasteOrderNumber = record.GetSolidWasteOrderNumber();
                SelectedStreet = record.GetSolidWasteStreet();
                SelectedStreetItem = !string.IsNullOrEmpty(SelectedStreet)
                    ? SelectionItem.FromStreet(SelectedStreet)
                    : null;
                SelectedSolidWasteType = record.GetSolidWasteType();
                SelectedProviderId = record.ProviderId;
                _listItem.ProviderId = record.ProviderId;
                SelectedProviderItem = SelectedProviderId.HasValue
                    ? SelectionItem.FromProvider(new ProviderDto { Id = SelectedProviderId.Value, ProviderName = Providers.FirstOrDefault(p => p.Id == SelectedProviderId.Value)?.ProviderName ?? string.Empty })
                    : null;

                // Read MaterialId and WaybillQuantity
                var materialId = record.GetProperty<int?>("SolidWasteInfo.MaterialId");
                var waybillQuantity = record.GetProperty<decimal?>("SolidWasteInfo.WaybillQuantity");

                if (materialId.HasValue)
                {
                    var material = SolidWasteMaterials.FirstOrDefault(m => m.Id == materialId.Value);
                    if (material != null)
                    {
                        SelectedSolidWasteMaterial = material;
                        SelectedMaterialItem = SelectionItem.FromMaterial(material);

                        // Load units and auto-select first
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

                // Read SolidWaste data from ExtraProperties
                SolidWasteOrderNumber = waybill.GetSolidWasteOrderNumber();
                SelectedStreet = waybill.GetSolidWasteStreet();
                SelectedStreetItem = !string.IsNullOrEmpty(SelectedStreet)
                    ? SelectionItem.FromStreet(SelectedStreet)
                    : null;
                SelectedSolidWasteType = waybill.GetSolidWasteType();
                SelectedProviderId = waybill.ProviderId;
                _listItem.ProviderId = waybill.ProviderId;
                SelectedProviderItem = SelectedProviderId.HasValue
                    ? SelectionItem.FromProvider(new ProviderDto
                    {
                        Id = SelectedProviderId.Value,
                        ProviderName = Providers.FirstOrDefault(p => p.Id == SelectedProviderId.Value)?.ProviderName
                            ?? string.Empty
                    })
                    : null;

                // Read MaterialId and WaybillQuantity
                var materialId = waybill.GetProperty<int?>("SolidWasteInfo.MaterialId");
                var waybillQuantity = waybill.GetProperty<decimal?>("SolidWasteInfo.WaybillQuantity");

                // If not in ExtraProperties, try reading from standard fields
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
                        SelectedMaterialItem = SelectionItem.FromMaterial(material);

                        // Load units and auto-select
                        var units = await LoadMaterialUnitsForRowAsync(material.Id);
                        if (MaterialItems.Count > 0)
                        {
                            var firstRow = MaterialItems[0];
                            firstRow.SetMaterialUnits(units);

                            // If Waybill has MaterialUnitId, use it; otherwise select first
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

    private async Task ApplyRecommendationAsync()
    {
        // Check if we need to fill missing fields
        var hasProviderId = SelectedProviderId.HasValue;
        var hasMaterialId = SelectedSolidWasteMaterial != null;
        var needsRecommendation = !hasProviderId || !hasMaterialId;

        WaybillRecommendationDto? recommendation = null;
        if (needsRecommendation)
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync();
                var enableLatestRecommendation = settings.SystemSettings.EnableLatestRecommendation;

                if (enableLatestRecommendation)
                {
                    recommendation = await _recommendationService.GetLatestRecommendationAsync();
                }
                else if (!string.IsNullOrWhiteSpace(PlateNumber))
                {
                    recommendation = await _recommendationService.GetRecommendationByPlateNumberAsync(PlateNumber);
                }

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

        // Apply recommendation data to fill missing fields
        if (recommendation != null)
        {
            if (!hasProviderId && recommendation.ProviderId.HasValue)
            {
                SelectedProviderId = recommendation.ProviderId.Value;
                _listItem.ProviderId = recommendation.ProviderId.Value;

                var provider = Providers.FirstOrDefault(p => p.Id == recommendation.ProviderId.Value);
                if (provider != null)
                {
                    SelectedProviderItem = SelectionItem.FromProvider(new ProviderDto
                    {
                        Id = provider.Id,
                        ProviderName = provider.ProviderName
                    });
                }
            }

            if (!hasMaterialId && recommendation.MaterialId.HasValue)
            {
                var material = SolidWasteMaterials.FirstOrDefault(m => m.Id == recommendation.MaterialId.Value);
                if (material != null)
                {
                    SelectedSolidWasteMaterial = material;
                    SelectedMaterialItem = SelectionItem.FromMaterial(material);

                    // Material change will trigger auto unit selection (via existing WhenAnyValue subscription)
                }
            }
        }
    }

    protected override async Task SaveModeSpecificAsync()
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
                null,
                IsWeighingRecord ? SelectedDeliveryType : null));
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

    protected override async Task CompleteModeSpecificAsync()
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
        // For now, we won't enforce selecting a solid waste type, as some scenarios may not require it.
        // if (string.IsNullOrEmpty(SelectedSolidWasteType))
        // {
        //     await ShowMessageBoxAsync("请先选择类型");
        //     throw new InvalidOperationException("请先选择类型");
        // }

        if (string.IsNullOrEmpty(SolidWasteOrderNumber))
        {
            await ShowMessageBoxAsync("请填写联单号");
            throw new InvalidOperationException("请填写联单号");
        }

        var weighingMatchingService = _serviceProvider.GetRequiredService<IWeighingMatchingService>();

        try
        {
            // Save solid waste mode data first
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

            // Then complete the order
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

    #region SolidWaste Selector Delegate Methods

    private async Task<PagedResultDto<SelectionItem>> LoadProvidersPageAsync(
        string? search, int pageIndex, int pageSize, IReadOnlyList<int>? selectedIds)
    {
        var result = await _providerService.GetPagedProvidersAsync(search, pageIndex, pageSize, selectedIds);
        var items = result.Items.Select(SelectionItem.FromProvider).ToList();
        return new PagedResultDto<SelectionItem>(result.TotalCount, items);
    }

    private async Task<PagedResultDto<SelectionItem>> LoadMaterialsPageAsync(
        string? search, int pageIndex, int pageSize, IReadOnlyList<int>? selectedIds)
    {
        var result = await _materialService.GetPagedMaterialsAsync(search, pageIndex, pageSize, selectedIds);
        var items = result.Items.Select(SelectionItem.FromMaterial).ToList();
        return new PagedResultDto<SelectionItem>(result.TotalCount, items);
    }

    private Task<PagedResultDto<SelectionItem>> LoadStreetsPageAsync(
        string? search, int pageIndex, int pageSize, IReadOnlyList<int>? selectedIds)
    {
        var allStreets = (_streetsConfig.Value.Streets ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct()
            .OrderBy(s => s)
            .Select(SelectionItem.FromStreet)
            .ToList();

        // Filter by search
        IEnumerable<SelectionItem> filtered = allStreets;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLowerInvariant();
            filtered = allStreets.Where(s => s.Name.ToLowerInvariant().Contains(searchLower));
        }

        // Ensure selectedIds items are included
        if (selectedIds is { Count: > 0 })
        {
            var selectedSet = new HashSet<int>(selectedIds);
            var selected = allStreets.Where(s => selectedSet.Contains(s.Id)).ToList();
            foreach (var item in selected)
            {
                if (!filtered.Any(f => f.Id == item.Id))
                    filtered = filtered.Prepend(item);
            }
        }

        var list = filtered.ToList();
        var total = list.Count;
        var page = list.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResultDto<SelectionItem>(total, page));
    }

    private async Task<SelectionItem?> CreateNewProviderAsync(string name)
    {
        var confirmed = await ConfirmTextInteraction.Handle(new ConfirmTextRequest(
            Title: "确认新增供应商",
            Message: "将新增一条供应商，请确认名称：",
            InitialValue: name));
        if (confirmed == null) return null;

        var deliveryType = _listItem?.DeliveryType ?? DeliveryType.Receiving;
        var created = await _providerService.CreateProviderAsync(confirmed, deliveryType);
        var dto = new ProviderDto
        {
            Id = created.Id,
            ProviderType = created.ProviderType ?? (int)deliveryType,
            ProviderName = created.ProviderName,
            ContactName = created.ContectName,
            ContactPhone = created.ContectPhone
        };
        return SelectionItem.FromProvider(dto);
    }

    private async Task<SelectionItem?> CreateNewMaterialAsync(string name)
    {
        var confirmed = await ConfirmTextInteraction.Handle(new ConfirmTextRequest(
            Title: "确认新增材料",
            Message: "将新增一条材料，请确认名称：",
            InitialValue: name));
        if (confirmed == null) return null;

        var created = await _materialService.CreateMaterialAsync(confirmed);
        if (created is Material material)
            return SelectionItem.FromMaterial(material);
        return null;
    }

    #endregion
}
