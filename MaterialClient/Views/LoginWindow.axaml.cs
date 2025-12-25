using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MaterialClient.ViewModels;
using ReactiveUI;

namespace MaterialClient.Views;

public partial class LoginWindow : Window
{
    private IDisposable? _loginSuccessSubscription;
    
    /// <summary>
    /// 公开的登录成功属性，用于在窗口关闭后读取
    /// </summary>
    public bool IsLoginSuccessful { get; private set; }

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
                            IsLoginSuccessful = isSuccessful;  // 保存到窗口属性
                            if (isSuccessful)
                            {
                                // 隐藏窗口而不是关闭，以便StartupService可以管理窗口生命周期
                                Dispatcher.UIThread.Post(async () =>
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(0.5));
                                    Hide();
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

