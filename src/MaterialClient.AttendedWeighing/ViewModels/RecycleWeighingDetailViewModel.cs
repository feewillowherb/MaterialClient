using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using MaterialClient.Common.Providers;
using MaterialClient.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.ViewModels;

public partial class RecycleWeighingDetailViewModel : AttendedWeighingDetailViewModelBase, ITransientDependency
{
    private readonly IRecommendationService _recommendationService;
    private readonly ISettingsService _settingsService;
    private readonly IRecycleWeighingService _recycleWeighingService;

    [Reactive] private ObservableCollection<Material> _recycleMaterials = new();
    [Reactive] private Material? _selectedRecycleMaterial;
    [Reactive] private SelectionItem? _selectedProviderItem;
    [Reactive] private SelectionItem? _selectedMaterialItem;

    /// <summary>单价（元/吨，可选）。§2.2 unitPrice 数据源，回填/持久化到 RecycleWaybillExtension。</summary>
    [Reactive] private decimal? _unitPrice;

    /// <summary>销售合同编号（可选）。§2.2 saleContractNo 数据源，回填/持久化到 RecycleWaybillExtension。</summary>
    [Reactive] private string? _saleContractNo;

    public Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<SelectionItem>>> ProviderLoadPageAsync { get; }
    public Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<SelectionItem>>> MaterialLoadPageAsync { get; }
    public Func<string, Task<SelectionItem?>>? ProviderCreateNewAsync { get; }
    public Func<string, Task<SelectionItem?>>? MaterialCreateNewAsync { get; }

    public override bool IsSolidWasteMode => false;

    public RecycleWeighingDetailViewModel(
        IServiceProvider serviceProvider,
        IRecycleWeighingService recycleWeighingService,
        IRecommendationService recommendationService,
        ISettingsService settingsService)
        : base(serviceProvider, serviceProvider.GetService<ILogger<RecycleWeighingDetailViewModel>>())
    {
        _recycleWeighingService = recycleWeighingService;
        _recommendationService = recommendationService;
        _settingsService = settingsService;

        ProviderLoadPageAsync = LoadProvidersPageAsync;
        MaterialLoadPageAsync = LoadMaterialsPageAsync;
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
                if (material != null) SelectedRecycleMaterial = material;
            });

        this.WhenAnyValue(x => x.SelectedRecycleMaterial)
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
        await LoadRecycleDataAsync();
        await ApplyRecommendationAsync();
    }

    private async Task LoadRecycleDataAsync()
    {
        try
        {
            RecycleMaterials.Clear();
            var materials = await _materialService.GetAllMaterialsAsync();
            foreach (var material in materials)
                RecycleMaterials.Add(material);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载材料列表失败");
        }

        try
        {
            var detail = await _recycleWeighingService.GetRecycleDetailAsync(_listItem.Id, _listItem.ItemType);
            if (detail == null)
            {
                return;
            }

            SelectedProviderId = detail.ProviderId;
            _listItem.ProviderId = detail.ProviderId;
            SelectedProviderItem = SelectedProviderId.HasValue
                ? SelectionItem.FromProvider(new ProviderDto
                {
                    Id = SelectedProviderId.Value,
                    ProviderName = Providers.FirstOrDefault(p => p.Id == SelectedProviderId.Value)?.ProviderName
                        ?? string.Empty
                })
                : null;

            UnitPrice = detail.UnitPrice;
            SaleContractNo = detail.SaleContractNo;
            if (detail.Remark != null)
            {
                Remark = detail.Remark;
            }

            if (!detail.MaterialId.HasValue)
            {
                return;
            }

            var material = RecycleMaterials.FirstOrDefault(m => m.Id == detail.MaterialId.Value);
            if (material == null)
            {
                return;
            }

            SelectedRecycleMaterial = material;
            SelectedMaterialItem = SelectionItem.FromMaterial(material);

            var units = await LoadMaterialUnitsForRowAsync(material.Id);
            if (MaterialItems.Count == 0)
            {
                return;
            }

            var firstRow = MaterialItems[0];
            firstRow.SetMaterialUnits(units);
            var unitId = detail.MaterialUnitId ?? units.FirstOrDefault()?.Id;
            if (unitId.HasValue)
                firstRow.InitializeSelection(material, units, unitId.Value);
            else if (units.Count > 0)
                firstRow.InitializeSelection(material, units, units[0].Id);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "加载 Recycle 模式数据失败");
        }
    }

    private async Task ApplyRecommendationAsync()
    {
        var hasProviderId = SelectedProviderId.HasValue;
        var hasMaterialId = SelectedRecycleMaterial != null;
        var needsRecommendation = !hasProviderId || !hasMaterialId;

        WaybillRecommendationDto? recommendation = null;
        if (needsRecommendation)
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync();
                var enableLatestRecommendation = settings.SystemSettings.EnableLatestRecommendation;

                if (enableLatestRecommendation)
                    recommendation = await _recommendationService.GetLatestRecommendationAsync();
                else if (!string.IsNullOrWhiteSpace(PlateNumber))
                    recommendation = await _recommendationService.GetRecommendationByPlateNumberAsync(PlateNumber);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "获取推荐数据失败");
            }
        }

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
                var material = RecycleMaterials.FirstOrDefault(m => m.Id == recommendation.MaterialId.Value);
                if (material != null)
                {
                    SelectedRecycleMaterial = material;
                    SelectedMaterialItem = SelectionItem.FromMaterial(material);
                }
            }
        }
    }

    protected override async Task SaveModeSpecificAsync()
    {
        var providerId = SelectedProviderId;
        var materialId = SelectedRecycleMaterial?.Id;
        var materialUnitId = MaterialItems.FirstOrDefault()?.SelectedMaterialUnit?.Id;

        try
        {
            await _recycleWeighingService.UpdateRecycleModeAsync(new UpdateRecycleModeInput(
                _listItem.Id,
                _listItem.ItemType,
                PlateNumber,
                providerId,
                materialId,
                materialUnitId,
                IsWeighingRecord ? SelectedDeliveryType : null,
                Remark,
                UnitPrice,
                SaleContractNo));
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
        var materialId = SelectedRecycleMaterial?.Id;
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

        var weighingMatchingService = _serviceProvider.GetRequiredService<IWeighingMatchingService>();

        try
        {
            await _recycleWeighingService.UpdateRecycleModeAsync(new UpdateRecycleModeInput(
                _listItem.Id,
                _listItem.ItemType,
                PlateNumber,
                providerId,
                materialId,
                materialUnitId,
                IsWeighingRecord ? SelectedDeliveryType : null,
                Remark,
                UnitPrice,
                SaleContractNo));

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

    #region Selector Delegate Methods

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

    private async Task<SelectionItem?> CreateNewProviderAsync(string name)
    {
        var result = await CreateProviderInteraction.Handle(new CreateProviderRequest(
            Title: "确认新增供应商",
            Message: "将新增一条供应商，请确认信息：",
            InitialName: name));
        if (result == null) return null;

        var deliveryType = _listItem?.DeliveryType ?? DeliveryType.Receiving;
        var created = await _providerService.CreateProviderAsync(result.Name, deliveryType, result.Address);
        var dto = new ProviderDto
        {
            Id = created.Id,
            ProviderType = created.ProviderType ?? (int)deliveryType,
            ProviderName = created.ProviderName,
            ContactName = created.ContectName,
            ContactPhone = created.ContectPhone,
            Address = created.Address
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
