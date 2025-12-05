using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MaterialClient.Views;

public partial class DetailWindow : Window
{
    public DetailWindow()
    {
        InitializeComponent();
    }

    private void Btn_Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
