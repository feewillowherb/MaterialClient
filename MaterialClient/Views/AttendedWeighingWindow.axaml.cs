using Avalonia.Controls;
using MaterialClient.ViewModels;

namespace MaterialClient.Views;

public partial class AttendedWeighingWindow : Window
{
    public AttendedWeighingWindow()
    {
        InitializeComponent();
        DataContext = new AttendedWeighingViewModel();
    }
}
