using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaterialClient.ViewModels;
using ReactiveUI;

namespace MaterialClient.Views;

public partial class LoginWindow : Window
{
    private IDisposable? _loginSuccessSubscription;

    public LoginWindow(LoginWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Subscribe to DataContext changes
        this.WhenAnyValue(x => x.DataContext)
            .Subscribe(dataContext =>
            {
                _loginSuccessSubscription?.Dispose();

                if (dataContext is LoginWindowViewModel viewModel)
                {
                    // Watch for successful login
                    _loginSuccessSubscription = viewModel
                        .WhenAnyValue(vm => vm.IsLoginSuccessful)
                        .Subscribe(isSuccessful =>
                        {
                            if (isSuccessful)
                            {
                                // Close window after a short delay to show success message
                                Dispatcher.UIThread.Post(async () =>
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(0.5));
                                    Close();
                                }, DispatcherPriority.Background);
                            }
                        });
                }
            });
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        // Close the application when login window is closed without successful login
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _loginSuccessSubscription?.Dispose();
        base.OnClosed(e);
    }
}

