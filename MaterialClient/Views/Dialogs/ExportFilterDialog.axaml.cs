using Avalonia.Controls;
using MaterialClient.ViewModels;

namespace MaterialClient.Views.Dialogs;

public partial class ExportFilterDialog : Window
{
    public ExportFilterDialog()
    {
        InitializeComponent();
    }

    public ExportFilterDialog(ExportFilterDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;

        viewModel.ExportCommand.Subscribe(_ => Close(viewModel));
        viewModel.CancelCommand.Subscribe(_ => Close(null));
    }
}
