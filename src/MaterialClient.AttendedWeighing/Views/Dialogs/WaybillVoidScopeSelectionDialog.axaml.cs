using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.ViewModels;

namespace MaterialClient.Views.Dialogs;

public partial class WaybillVoidScopeSelectionDialog : UserControl
{
    public WaybillVoidScopeSelectionDialog()
    {
        InitializeComponent();
    }

    private void OnScopeChecked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not WaybillVoidScopeSelectionViewModel vm) return;

        if (sender is RadioButton { IsChecked: true } radioButton)
        {
            vm.SelectedScope = radioButton.Name switch
            {
                nameof(JoinOnlyRadio) => WaybillVoidScope.JoinOnly,
                nameof(OutOnlyRadio) => WaybillVoidScope.OutOnly,
                nameof(BothRadio) => WaybillVoidScope.Both,
                _ => vm.SelectedScope
            };
        }
    }
}
