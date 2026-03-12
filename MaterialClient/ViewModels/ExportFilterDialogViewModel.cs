using System;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.ViewModels;

public partial class ExportFilterDialogViewModel : ViewModelBase
{
    [Reactive] private DateTime? _startDate;
    [Reactive] private DateTime? _endDate;
    [Reactive] private string? _plateNumber;

    public bool Confirmed { get; private set; }

    [ReactiveCommand]
    private void Export()
    {
        Confirmed = true;
    }

    [ReactiveCommand]
    private void Cancel()
    {
        Confirmed = false;
    }
}
