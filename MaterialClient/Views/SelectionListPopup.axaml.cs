using Avalonia.Controls;
using Avalonia.Input;
using MaterialClient.ViewModels;
using System.Linq;

namespace MaterialClient.Views;

/// <summary>
/// Simplified selection popup without internal search box.
/// Search happens in the SearchableComboBox trigger component.
/// </summary>
public partial class SelectionListPopup : UserControl
{
    public SelectionListPopup()
    {
        InitializeComponent();
    }

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is DataGrid dataGrid &&
            dataGrid.SelectedItem != null &&
            DataContext is ISearchableSelection<object> viewModel)
        {
            viewModel.SelectItemCommand.Execute(dataGrid.SelectedItem);
        }
        // Support old interface for backward compatibility
        else if (sender is DataGrid oldDataGrid &&
            oldDataGrid.SelectedItem != null &&
            DataContext is IGenericSelectionPopupViewModel oldViewModel)
        {
            oldViewModel.SelectItemCommand.Execute(oldDataGrid.SelectedItem);
        }
    }

    private void OnItemSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Try new interface first
        if (DataContext is ISearchableSelection<object> viewModel)
        {
            var selected = e.AddedItems?.Cast<object?>().FirstOrDefault(x => x != null);
            if (selected != null)
            {
                viewModel.SelectItemCommand.Execute(selected);
            }
        }
        // Fallback to old interface for backward compatibility
        else if (DataContext is IGenericSelectionPopupViewModel oldViewModel)
        {
            var selected = e.AddedItems?.Cast<object?>().FirstOrDefault(x => x != null);
            if (selected != null)
            {
                oldViewModel.SelectItemCommand.Execute(selected);
            }
        }
    }
}
