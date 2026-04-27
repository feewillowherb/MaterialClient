using Avalonia.Controls;
using MaterialClient.Demo.ViewModels;

namespace MaterialClient.Demo.Views;

public partial class SchemeBWindow : Window
{
    public SchemeBWindow()
    {
        InitializeComponent();
        DemoDataGrid.ItemsSource = DemoDataGenerator.GetDemoRecords();
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
