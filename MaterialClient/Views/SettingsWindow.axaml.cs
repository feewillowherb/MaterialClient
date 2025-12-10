using System;
using Avalonia.Controls;
using MaterialClient.ViewModels;

namespace MaterialClient.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Subscribe to close requested event
        viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from event
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            viewModel.CloseRequested -= OnCloseRequested;
        }
        base.OnClosed(e);
    }
}
