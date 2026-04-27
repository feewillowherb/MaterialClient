using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MaterialClient.Demo.ViewModels;

namespace MaterialClient.Demo.Views;

public partial class SchemeDWindow : Window
{
    public SchemeDWindow()
    {
        InitializeComponent();
        DemoDataGrid.ItemsSource = DemoDataGenerator.GetDemoRecords();
        DemoDataGrid.SelectionChanged += OnSelectionChanged;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedItem = DemoDataGrid.SelectedItem;
        foreach (var row in DemoDataGrid.GetVisualDescendants().OfType<DataGridRow>())
        {
            var checkIcon = row.FindControl<Avalonia.Controls.Shapes.Path>("CheckIcon");
            if (checkIcon is not null)
            {
                checkIcon.IsVisible = ReferenceEquals(row.DataContext, selectedItem);
            }
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
