using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MaterialClient.ViewModels;

namespace MaterialClient.Views.Dialogs;

public partial class RecycleReceivingWindow : Window
{
    public RecycleReceivingWindow()
    {
        InitializeComponent();
    }

    public RecycleReceivingWindow(RecycleReceivingViewModel viewModel) : this()
    {
        DataContext = viewModel;

        viewModel.SetImagePickerHandler(async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择收货照片",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("图片")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp" }
                    }
                }
            });
            return files.Count > 0 ? files[0].Path.LocalPath : null;
        });

        viewModel.ConfirmCommand.Subscribe(_ =>
        {
            if (viewModel.Result != null)
            {
                Close(viewModel.Result);
            }
        });
        viewModel.CancelCommand.Subscribe(_ => Close(null));
    }
}
