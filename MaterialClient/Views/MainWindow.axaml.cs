using Avalonia.Controls;
using MaterialClient.ViewModels;

namespace MaterialClient.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}