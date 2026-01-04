using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaterialClient.ViewModels;
using ReactiveUI;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Views;

public partial class AuthCodeWindow : Window, ITransientDependency
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
                    // Watch for successful authorization
                    _authSuccessSubscription = viewModel
                        .WhenAnyValue(vm => vm.IsVerified)
                        .Subscribe(isVerified =>
                        {
                            IsVerified = isVerified; // 保存到窗口属性
                            if (isVerified)
                                Dispatcher.UIThread.Post(async () =>
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(0.5));
                                    // 隐藏窗口而不是关闭，以便StartupService可以管理窗口生命周期
                                    Hide();
                                }, DispatcherPriority.Background);
                        });
            });
    }

    /// <summary>
    ///     公开的验证结果属性，用于在窗口关闭后读取
    /// </summary>
    public bool IsVerified { get; private set; }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        // When user closes the window without completing authorization,
        // the application should exit (as per FR-003)
        if (DataContext is AuthCodeWindowViewModel viewModel) viewModel.HandleWindowClose();

        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _authSuccessSubscription?.Dispose();
        base.OnClosed(e);
    }
}