using Avalonia.Controls;
using Avalonia.Input;
using MaterialClient.ViewModels;
using System.Linq;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views;

/// <summary>
/// [OBSOLETE] This component is replaced by SelectionListPopup.
/// This component will be removed in a future version.
/// Please migrate to SearchableComboBox + SelectionListPopup for new development.
/// Migration guide: Use SearchableSelectionViewModel<T> with SelectionListPopup instead.
/// </summary>
[Obsolete("GenericSelectionPopup is obsolete. Use SearchableComboBox + SelectionListPopup instead. This will be removed in v2.5.")]
public partial class GenericSelectionPopup : UserControl, ITransientDependency
{
    public GenericSelectionPopup()
    {
        InitializeComponent();
    }

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is DataGrid dataGrid &&
            dataGrid.SelectedItem != null &&
            DataContext is IGenericSelectionPopupViewModel viewModel)
        {
            viewModel.SelectItemCommand.Execute(dataGrid.SelectedItem);
        }
    }

    private void OnItemSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not IGenericSelectionPopupViewModel viewModel) return;

        var selected = e.AddedItems?.Cast<object?>().FirstOrDefault(x => x != null);
        if (selected != null)
        {
            viewModel.SelectItemCommand.Execute(selected);
        }
    }
}

