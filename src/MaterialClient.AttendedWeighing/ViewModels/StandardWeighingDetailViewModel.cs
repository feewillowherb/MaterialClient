using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

using WeighingListItemMaterialDto = MaterialClient.Common.Models.WeighingListItemMaterialDto;

namespace MaterialClient.ViewModels;

/// <summary>
///     Standard (non-SolidWaste) weighing detail view model.
///     Handles material selection with popup-based UI and standard save/complete logic.
/// </summary>
public partial class StandardWeighingDetailViewModel : AttendedWeighingDetailViewModelBase, ITransientDependency
{
    [Reactive] private bool _isMaterialPopupOpen;
    [Reactive] private MaterialItemRow? _currentMaterialRow;
    [Reactive] private MaterialsSelectionPopupViewModel? _materialsSelectionPopupViewModel;

    private readonly IRecommendationService _recommendationService;
    private readonly ISettingsService _settingsService;
    private IDisposable? _materialSelectionSubscription;

    public override bool IsSolidWasteMode => false;

    public StandardWeighingDetailViewModel(
        IServiceProvider serviceProvider,
        IRecommendationService recommendationService,
        ISettingsService settingsService)
        : base(serviceProvider, serviceProvider.GetService<ILogger<StandardWeighingDetailViewModel>>())
    {
        _recommendationService = recommendationService;
        _settingsService = settingsService;
        InitializeMaterialsSelectionPopup();
    }

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

    protected override async Task LoadModeSpecificDataAsync()
    {
        // Recommendation system: check if we need to fill missing fields
        var firstMaterialDto = _listItem.Materials.Count > 0 ? _listItem.Materials[0] : null;
        var hasMaterialId = firstMaterialDto?.MaterialId.HasValue ?? _listItem.MaterialId.HasValue;
        var hasMaterialUnitId = firstMaterialDto?.MaterialUnitId.HasValue ?? _listItem.MaterialUnitId.HasValue;
        var hasProviderId = _listItem.ProviderId.HasValue;

        var needsRecommendation = !hasMaterialId || !hasMaterialUnitId || !hasProviderId;

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
                else if (hasMaterialId && !hasMaterialUnitId && recommendation.MaterialUnitId.HasValue)
                {
                    if (_listItem.Materials.Count > 0)
                    {
                        _listItem.Materials[0].MaterialUnitId = recommendation.MaterialUnitId;
                    }
                }
            }
        }

        // Set SelectedProvider from the shared Providers collection
        if (SelectedProviderId.HasValue)
        {
            var provider = Providers.FirstOrDefault(p => p.Id == SelectedProviderId.Value);
            if (provider != null)
            {
                SelectedProvider = provider;
            }
        }

        // Initialize each MaterialItemRow from _listItem.Materials
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

    protected override async Task SaveModeSpecificAsync()
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

    protected override async Task CompleteModeSpecificAsync()
    {
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

        // Update data first
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

        // Then complete the order
        await weighingMatchingService.CompleteOrderAsync(_listItem.Id);
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
        CloseMaterialPopup();

        if (MaterialsSelectionPopupViewModel != null)
        {
            MaterialsSelectionPopupViewModel.SelectedMaterial = null;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Closes the material selection popup and clears the current row without selecting a material.
    ///     Idempotent: safe to call when the popup is already closed.
    /// </summary>
    public void CloseMaterialPopup()
    {
        IsMaterialPopupOpen = false;
        CurrentMaterialRow = null;
    }
}
