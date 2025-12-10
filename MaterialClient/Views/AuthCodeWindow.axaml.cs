using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using MaterialClient.ViewModels;
using ReactiveUI;

namespace MaterialClient.Views;

public partial class AuthCodeWindow : Window
{
    private IDisposable? _authSuccessSubscription;

    public AuthCodeWindow(AuthCodeWindowViewModel authCodeWindowViewModel)
    {
        InitializeComponent();
        DataContext = authCodeWindowViewModel;

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
                        .Subscribe(async isVerified =>
                        {
                            if (isVerified)
                            {
                                await Task.Delay(1000);
                                Close();
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

