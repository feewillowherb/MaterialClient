using Irihi.Avalonia.Shared.Contracts;
using MaterialClient.Common.Entities.Enums;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace MaterialClient.ViewModels;

/// <summary>
///     ViewModel for the waybill void scope selection dialog
/// </summary>
public partial class WaybillVoidScopeSelectionViewModel : ViewModelBase, IDialogContext
{
    /// <summary>
    ///     The selected void scope, null when no selection is made
    /// </summary>
    [Reactive]
    private WaybillVoidScope? _selectedScope;

    /// <summary>
    ///     Whether a scope has been selected (used to enable confirm button)
    /// </summary>
    public bool HasSelection => SelectedScope.HasValue;

    /// <summary>
    ///     Command to confirm the selection and close the dialog
    /// </summary>
    [ReactiveCommand(CanExecute = nameof(HasSelection))]
    private void Confirm()
    {
        RequestClose?.Invoke(this, (object?)SelectedScope);
    }

    /// <summary>
    ///     Command to cancel and close the dialog
    /// </summary>
    [ReactiveCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, null);
    }

    /// <inheritdoc />
    public event EventHandler<object?>? RequestClose;

    /// <inheritdoc />
    public void Close()
    {
        RequestClose?.Invoke(this, null);
    }
}
