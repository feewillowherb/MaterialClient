using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialClient.ViewModels;

namespace MaterialClient.Views;

public partial class AuthCodeWindow : Window
{
    public AuthCodeWindow()
    {
        InitializeComponent();
    }
    
    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        // When user closes the window without completing authorization,
        // the application should exit (as per FR-003)
        if (DataContext is AuthCodeWindowViewModel viewModel)
        {
            viewModel.HandleWindowClose();
        }
        Close();
    }
}

