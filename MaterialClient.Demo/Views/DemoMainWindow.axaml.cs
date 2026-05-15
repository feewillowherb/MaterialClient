using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace MaterialClient.Demo.Views;

public partial class DemoMainWindow : Window
{
    public DemoMainWindow()
    {
        InitializeComponent();
    }

    private void OnSchemeAClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        new SchemeAWindow().ShowDialog(this);
    }

    private void OnSchemeBClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        new SchemeBWindow().ShowDialog(this);
    }

    private void OnSchemeCClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        new SchemeCWindow().ShowDialog(this);
    }

    private void OnSchemeDClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        new SchemeDWindow().ShowDialog(this);
    }

    private void OnToggleThemeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var app = Application.Current!;
        app.RequestedThemeVariant = app.RequestedThemeVariant == ThemeVariant.Light
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
    }

    private void OnWeighingSystemClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        new WeighingSystemWindow().ShowDialog(this);
    }
}
