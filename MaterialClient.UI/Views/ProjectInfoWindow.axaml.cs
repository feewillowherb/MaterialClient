using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialClient.UI.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.Views;

public partial class ProjectInfoWindow : Window, ITransientDependency
{
    public ProjectInfoWindow(ProjectInfoWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
