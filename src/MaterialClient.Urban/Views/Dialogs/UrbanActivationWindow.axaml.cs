using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialClient.Urban.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Views.Dialogs;

public partial class UrbanActivationWindow : Window, ITransientDependency
{
    public UrbanActivationWindow()
    {
        InitializeComponent();
    }

    public UrbanActivationWindow(UrbanActivationWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.ActivationSucceeded += (_, _) => Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
