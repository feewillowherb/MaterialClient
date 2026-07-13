using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaterialClient.UI.ViewModels;
using ReactiveUI;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.Views;

public partial class AuthCodeWindow : Window, ITransientDependency
{
    private IDisposable? _authSuccessSubscription;

    public AuthCodeWindow(AuthCodeWindowViewModel authCodeWindowViewModel)
    {
        InitializeComponent();
        DataContext = authCodeWindowViewModel;

        this.WhenAnyValue(x => x.DataContext)
            .Subscribe(dataContext =>
            {
                _authSuccessSubscription?.Dispose();

                if (dataContext is AuthCodeWindowViewModel viewModel)
                    _authSuccessSubscription = viewModel
                        .WhenAnyValue(vm => vm.IsVerified)
                        .Subscribe(isVerified =>
                        {
                            IsVerified = isVerified;
                            if (isVerified)
                                Dispatcher.UIThread.Post(async () =>
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(0.5));
                                    Hide();
                                }, DispatcherPriority.Background);
                        });
            });
    }

    public bool IsVerified { get; private set; }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AuthCodeWindowViewModel viewModel)
        {
            viewModel.HandleWindowClose();
        }

        Close();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is AuthCodeWindowViewModel viewModel)
        {
            _ = viewModel.LoadCurrentDefaultWeighingModeAsync();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _authSuccessSubscription?.Dispose();
        base.OnClosed(e);
    }
}
