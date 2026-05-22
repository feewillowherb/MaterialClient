using Avalonia.Controls;
using MaterialClient.ViewModels;

namespace MaterialClient.Views.AttendedWeighing;

public partial class ProviderEditWindow : Window
{
    public ProviderEditWindow()
    {
        InitializeComponent();
    }

    public ProviderEditWindow(ProviderEditWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;

        viewModel.SaveCommand.Subscribe(_ => Close(viewModel.Result));
        viewModel.CancelCommand.Subscribe(_ => Close(null));
    }
}

