using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaterialClient.ViewModels;
using ReactiveUI;

namespace MaterialClient.Views;

public partial class AuthCodeWindow : Window
{
    private IDisposable? _authSuccessSubscription;

    public AuthCodeWindow(AuthCodeWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        // Subscribe to DataContext changes
        this.WhenAnyValue(x => x.DataContext)
            .Subscribe(dataContext =>
            {
                _authSuccessSubscription?.Dispose();
                
                if (dataContext is AuthCodeWindowViewModel viewModel)
                {
                    // Watch for successful authorization
                    _authSuccessSubscription = viewModel
                        .WhenAnyValue(vm => vm.IsVerified)
                        .Subscribe(isVerified =>
                        {
                            if (isVerified)
                            {
                                // Close window after a short delay to show success message
                                Dispatcher.UIThread.Post(async () =>
                                {
                                    await System.Threading.Tasks.Task.Delay(1000);
                                    Close();
                                }, DispatcherPriority.Background);
                            }
                        });
                }
            });
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

    protected override void OnClosed(EventArgs e)
    {
        _authSuccessSubscription?.Dispose();
        base.OnClosed(e);
    }
}

