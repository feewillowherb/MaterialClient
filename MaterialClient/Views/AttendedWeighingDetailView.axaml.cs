using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MaterialClient.Views;

public partial class AttendedWeighingDetailView : Window
{
    public AttendedWeighingDetailView()
    {
        InitializeComponent();
    }

    private void Btn_Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
