using Avalonia.Controls;
using MaterialClient.ViewModels;

namespace MaterialClient.Views.Dialogs;

public partial class AddCameraDialog : Window
{
    public AddCameraDialog()
    {
        InitializeComponent();
    }

    public AddCameraDialog(AddCameraDialogViewModel viewModel) : this()
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
