using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaterialClient.UI.ViewModels;
using ReactiveUI;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.Views;

public partial class LoginWindow : Window, ITransientDependency
{
    private IDisposable? _loginSuccessSubscription;

    public LoginWindow(LoginWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        this.WhenAnyValue(x => x.DataContext)
            .Subscribe(dataContext =>
            {
                _loginSuccessSubscription?.Dispose();

                if (dataContext is LoginWindowViewModel viewModel)
                    _loginSuccessSubscription = viewModel
                        .WhenAnyValue(vm => vm.IsLoginSuccessful)
                        .Subscribe(isSuccessful =>
                        {
                            IsLoginSuccessful = isSuccessful;
                            if (isSuccessful)
                                Dispatcher.UIThread.Post(async () =>
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(0.5));
                                    Hide();
                                }, DispatcherPriority.Background);
                        });
            });
    }

    public bool IsLoginSuccessful { get; private set; }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _loginSuccessSubscription?.Dispose();
        base.OnClosed(e);
    }
}
