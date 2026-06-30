using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialClient.Urban.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Views.Dialogs;

public partial class UrbanActivationWindow : Window, ITransientDependency
{
    public bool ActivationResult { get; private set; }

    public UrbanActivationWindow()
    {
        InitializeComponent();
    }

    public UrbanActivationWindow(UrbanActivationWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.ActivationSucceeded += (_, _) =>
        {
            ActivationResult = true;
            Close(true);
        };
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        ActivationResult = false;
        Close(false);
    }
}
