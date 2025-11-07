using Avalonia.Controls;
using Avalonia.Interactivity;
using MaterialClient.ViewModels;

namespace MaterialClient.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
    }
    
    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        // Close the application when login window is closed
        Close();
    }
}

