using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialClient.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views;

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
