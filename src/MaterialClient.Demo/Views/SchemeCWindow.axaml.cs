using Avalonia.Controls;
using MaterialClient.Demo.ViewModels;

namespace MaterialClient.Demo.Views;

public partial class SchemeCWindow : Window
{
    public SchemeCWindow()
    {
        InitializeComponent();
        DemoDataGrid.ItemsSource = DemoDataGenerator.GetDemoRecords();
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
