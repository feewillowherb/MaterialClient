using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
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

    public MaterialItemRow()
    {
        this.WhenAnyValue(x => x.SelectedMaterial)
            .Subscribe(value =>
            {
                if (value != null && LoadMaterialUnitsFunc != null)
                {
                    _ = LoadMaterialUnitsSafelyAsync(value.Id);
                }
                else
                {
                    MaterialUnits.Clear();
                    SelectedMaterialUnit = null;
                }

                if (IsWaybill) CalculateMaterialWeight();
            });

        this.WhenAnyValue(x => x.SelectedMaterialUnit)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(RateDisplay));
                if (IsWaybill) CalculateMaterialWeight();
            });

        this.WhenAnyValue(x => x.WaybillQuantity)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(WaybillQuantityDisplay));
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
                if (IsWaybill) CalculateMaterialWeight();
            });

        this.WhenAnyValue(x => x.Difference)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(DifferenceDisplay)));

        this.WhenAnyValue(x => x.DeviationRate)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(DeviationRateDisplay)));
    }

    public Func<int, Task<ObservableCollection<MaterialUnitDto>>>? LoadMaterialUnitsFunc { get; set; }

    public bool IsWaybill { get; set; }

    public string WaybillQuantityDisplay => WaybillQuantity?.ToString("F2") ?? "";
    public string WaybillWeightDisplay => WaybillWeight?.ToString("F2") ?? "";
    public string ActualQuantityDisplay => ActualQuantity?.ToString("F2") ?? "";
    public string ActualWeightDisplay => ActualWeight?.ToString("F2") ?? "";
    public string DifferenceDisplay => Difference?.ToString("F2") ?? "-";
    public string DeviationRateDisplay => DeviationRate.HasValue ? $"{DeviationRate.Value:F2}%" : "-";
    public string RateDisplay => SelectedMaterialUnit?.Rate.ToString("F2") ?? "";

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

                if (MaterialUnits.Count > 0 && SelectedMaterialUnit == null)
                {
                    SelectedMaterialUnit = MaterialUnits[0];
                }
            }
            catch (Exception)
            {
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

    public void ApplyCalculation(MaterialCalculation calc)
    {
        WaybillWeight = calc.PlanWeight;
        ActualQuantity = calc.ActualQuantity;
        Difference = calc.Difference;
        DeviationRate = calc.DeviationRate;
        DeviationResult = calc.OffsetResultDisplay;
    }
}
