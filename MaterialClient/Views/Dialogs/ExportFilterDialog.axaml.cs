using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
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

        viewModel.SetBrowseHandler(async () =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { Title = "选择导出目录" });
            return folders.Count > 0 ? folders[0].Path.LocalPath : null;
        });

        viewModel.ExportCommand.Subscribe(_ =>
        {
            if (viewModel.Confirmed) Close(viewModel);
        });
        viewModel.CancelCommand.Subscribe(_ => Close(null));
    }
}
