using Avalonia.Controls;
using MaterialClient.Urban.ViewModels;

namespace MaterialClient.Urban.Views.Dialogs;

public partial class WeighingRecordEditDialog : Window
{
    public WeighingRecordEditDialog()
    {
        InitializeComponent();
    }

    public WeighingRecordEditDialog(WeighingRecordEditDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;

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
