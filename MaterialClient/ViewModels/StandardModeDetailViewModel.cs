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
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MaterialClient.ViewModels;

/// <summary>
///     标准模式详情窗口 ViewModel
/// </summary>
public partial class StandardModeDetailViewModel : AttendedWeighingDetailViewModelBase, ITransientDependency
{
    private readonly IMaterialService _materialService;
    private readonly IProviderService _providerService;

    private IDisposable? _materialSelectionSubscription;

    public StandardModeDetailViewModel(
        IServiceProvider serviceProvider)
        : base(serviceProvider, serviceProvider.GetService<ILogger<StandardModeDetailViewModel>>())
    {
        _materialService = serviceProvider.GetRequiredService<IMaterialService>();
        _providerService = serviceProvider.GetRequiredService<IProviderService>();

        // 初始化材料选择弹窗 ViewModel
        InitializeMaterialsSelectionPopup();
    }

    #region 标准模式专用属性

    [Reactive] private ObservableCollection<ProviderDto> _providers = new();

    [Reactive] private ProviderDto? _selectedProvider;

    [Reactive] private ObservableCollection<Material> _materials = new();

    [Reactive] private bool _isMaterialPopupOpen;

    [Reactive] private MaterialItemRow? _currentMaterialRow;

    [Reactive] private MaterialsSelectionPopupViewModel? _materialsSelectionPopupViewModel;

    #endregion

    #region 初始化

    private void InitializeMaterialsSelectionPopup()
    {
        MaterialsSelectionPopupViewModel = new MaterialsSelectionPopupViewModel(_serviceProvider);

        _materialSelectionSubscription = MaterialsSelectionPopupViewModel.WhenAnyValue(x => x.SelectedMaterial)
            .Where(material => material != null)
            .Subscribe(material =>
            {
                if (material != null)
                {
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

        this.WhenAnyValue(x => x.IsMaterialPopupOpen)
            .Subscribe(isOpen =>
            {
                if (isOpen && MaterialsSelectionPopupViewModel != null)
                {
                    MaterialsSelectionPopupViewModel.SelectedMaterial = null;
                    _ = MaterialsSelectionPopupViewModel.RefreshAsync();
                }
            });
    }

    protected override async Task LoadDropdownDataAsync()
    {
        try
        {
            await Task.WhenAll(
                LoadProvidersAsync(),
                LoadMaterialsAsync()
            );

            // 检查是否需要获取推荐数据
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
                if (!hasProviderId && recommendation.ProviderId.HasValue)
                {
                    SelectedProviderId = recommendation.ProviderId.Value;
                    _listItem.ProviderId = recommendation.ProviderId.Value;
                }

                if (MaterialItems.Count > 0)
                {
                    if (!hasMaterialId && recommendation.MaterialId.HasValue)
                    {
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
                        }
                    }
                }
                else if (hasMaterialId && !hasMaterialUnitId && recommendation.MaterialUnitId.HasValue)
                {
                    if (_listItem.Materials.Count > 0)
                    {
                        _listItem.Materials[0].MaterialUnitId = recommendation.MaterialUnitId;
                    }
                }
            }

            if (SelectedProviderId.HasValue)
            {
                var provider = Providers.FirstOrDefault(p => p.Id == SelectedProviderId.Value);
                if (provider != null)
                {
                    SelectedProvider = provider;
                }
            }

            // 根据 _listItem.Materials 初始化每个 MaterialItemRow
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

    #region 命令

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

        if (MaterialsSelectionPopupViewModel != null)
        {
            MaterialsSelectionPopupViewModel.SelectedMaterial = null;
        }

        return Task.CompletedTask;
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
    }

    #endregion

    #region 抽象方法实现

    protected override async Task SaveCoreAsync()
    {
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
            IsWeighingRecord ? SelectedDeliveryType : null,
            Remark
        ));
    }

    protected override async Task CompleteCoreAsync()
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
            IsWeighingRecord ? SelectedDeliveryType : null,
            Remark
        ));

        // 然后完成订单
        await weighingMatchingService.CompleteOrderAsync(_listItem.Id);
    }

    #endregion
}
