using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.ViewModels;

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

    [Reactive] private bool _isWaybill;

    /// <summary>
    ///     加载材料单位的函数（由外部提供）
    /// </summary>
    public Func<int, Task<ObservableCollection<MaterialUnitDto>>>? LoadMaterialUnitsFunc;

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

    public string WaybillQuantityDisplay => WaybillQuantity?.ToString("F2") ?? "";
    public string WaybillWeightDisplay => WaybillWeight?.ToString("F2") ?? "";
    public string ActualQuantityDisplay => ActualQuantity?.ToString("F2") ?? "";
    public string ActualWeightDisplay => ActualWeight?.ToString("F2") ?? "";
    public string DifferenceDisplay => Difference?.ToString("F2") ?? "-";
    public string DeviationRateDisplay => DeviationRate.HasValue ? $"{DeviationRate.Value:F2}%" : "-";
    public string RateDisplay => SelectedMaterialUnit?.Rate.ToString("F2") ?? "";

    /// <summary>
    ///     计算物料重量（使用 MaterialCalculation 统一计算逻辑)
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
