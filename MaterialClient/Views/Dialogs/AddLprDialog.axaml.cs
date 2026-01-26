using Avalonia.Controls;
using MaterialClient.ViewModels;

namespace MaterialClient.Views.Dialogs;

public partial class AddLprDialog : Window
{
    public AddLprDialog()
    {
        InitializeComponent();
    }

    public AddLprDialog(AddLprDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        
        // Handle save/cancel commands
        if (viewModel != null)
        {
            viewModel.SaveCommand.Subscribe(_ =>
            {
                Close(viewModel.Result);
            });
            
            viewModel.CancelCommand.Subscribe(_ =>
            {
                Close(null);
            });
        }
    }
}
