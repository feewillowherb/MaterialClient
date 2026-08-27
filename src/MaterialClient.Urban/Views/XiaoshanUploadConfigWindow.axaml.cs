using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialClient.Urban.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Views;

public partial class XiaoshanUploadConfigWindow : Window, ITransientDependency
{
    public XiaoshanUploadConfigWindow(XiaoshanUploadConfigWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public XiaoshanUploadConfigWindowViewModel? ViewModel => DataContext as XiaoshanUploadConfigWindowViewModel;

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e) => Close();
}
