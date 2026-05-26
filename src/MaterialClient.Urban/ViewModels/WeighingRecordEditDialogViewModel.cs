using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.Urban.ViewModels;

/// <summary>
///     ViewModel for WeighingRecordEditDialog - allows editing PlateNumber and TotalWeight during approval
/// </summary>
public partial class WeighingRecordEditDialogViewModel : ReactiveObject
{
    [Reactive] private string _plateNumber = string.Empty;
    [Reactive] private string _totalWeight = string.Empty;

    public EditResult? Result { get; private set; }

    [ReactiveCommand]
    private void Save()
    {
        if (!decimal.TryParse(TotalWeight, out var weight) || weight < 0)
        {
            return;
        }

        Result = new EditResult(PlateNumber, weight);
    }

    [ReactiveCommand]
    private void Cancel()
    {
        Result = null;
    }
}

/// <summary>
///     Result from the weighing record edit dialog
/// </summary>
public record EditResult(string PlateNumber, decimal TotalWeight);
