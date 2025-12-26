using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Views.AttendedWeighing;
using MaterialClient.ViewModels;
using MaterialClient.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace MaterialClient.Services;

/// <summary>
/// 应用启动服务
/// 负责协调授权检查、登录流程和主窗口显示
/// </summary>
public class StartupService(
    ILicenseService licenseService,
    IAuthenticationService authenticationService,
    IServiceProvider serviceProvider,
    ILogger<StartupService>? logger = null)
{
    // 在启动时创建的三个窗口
    private AuthCodeWindow? _authCodeWindow;
    private LoginWindow? _loginWindow;
    private AttendedWeighingWindow? _attendedWeighingWindow;

    /// <summary>
    /// 执行启动流程
    /// 1. 在启动时同时创建三个窗口
    /// 2. 检查授权状态
    /// 3. 如果无授权或授权过期，显示授权窗口（隐藏另外两个）
    /// 4. 显示登录窗口（隐藏AttendedWeighingWindow）
    /// 5. 成功登录后显示有人值守过磅窗口
    /// </summary>
    public async Task<AttendedWeighingWindow?> StartupAsync()
    {
        try
        {
            // Step 0: 在启动时同时创建三个窗口（但不显示）
            _authCodeWindow = serviceProvider.GetRequiredService<AuthCodeWindow>();
            _loginWindow = serviceProvider.GetRequiredService<LoginWindow>();
            _attendedWeighingWindow = serviceProvider.GetRequiredService<AttendedWeighingWindow>();

            // 初始状态：所有窗口都隐藏
            _authCodeWindow.Hide();
            _loginWindow.Hide();
            _attendedWeighingWindow.Hide();

            // Step 1: Check license
            var isLicenseValid = await licenseService.IsLicenseValidAsync();

            bool licenseWasInvalid = !isLicenseValid;

            if (!isLicenseValid)
            {
                // No license or expired license - show authorization window
                var authResult = await ShowAuthorizationWindowAsync();
                if (!authResult)
                {
                    // User closed authorization window or authorization failed
                    // Application will exit
                    return null;
                }
            }

            // Step 2: Check for active session
            // 如果刚刚完成授权验证，清除旧的会话，要求重新登录
            if (licenseWasInvalid)
            {
                await authenticationService.LogoutAsync();
            }

            var hasActiveSession = await authenticationService.HasActiveSessionAsync();

            if (!hasActiveSession)
            {
                // No active session - show login window
                var loginResult = await ShowLoginWindowAsync();
                if (!loginResult)
                {
                    // User closed login window or login failed
                    // Application will exit
                    return null;
                }
            }

            // Step 3: Show attended weighing window (有人值守过磅窗口)
            ShowAttendedWeighingWindow();

            return _attendedWeighingWindow;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "启动失败");
            return null;
        }
    }

    private async Task<bool> ShowAuthorizationWindowAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        if (_authCodeWindow == null)
        {
            return false;
        }

        // 显示 AuthCodeWindow，隐藏另外两个
        _authCodeWindow.Show();
        if (_loginWindow != null)
        {
            _loginWindow.Hide();
        }

        if (_attendedWeighingWindow != null)
        {
            _attendedWeighingWindow.Hide();
        }

        IDisposable? verifiedSubscription = null;

        // 监听验证成功事件（通过ViewModel的IsVerified属性变化）
        if (_authCodeWindow.DataContext is AuthCodeWindowViewModel viewModel)
        {
            verifiedSubscription = viewModel
                .WhenAnyValue(vm => vm.IsVerified)
                .Where(isVerified => isVerified)
                .Subscribe(_ =>
                {
                    // 验证成功，隐藏AuthCodeWindow，显示LoginWindow
                    _authCodeWindow?.Hide();
                    if (_loginWindow != null)
                    {
                        _loginWindow.Show();
                    }

                    verifiedSubscription?.Dispose();
                    tcs.TrySetResult(true);
                });
        }

        // 监听窗口关闭事件（用户点击关闭按钮）
        void OnWindowClosed(object? sender, EventArgs args)
        {
            if (_authCodeWindow != null)
            {
                _authCodeWindow.Closed -= OnWindowClosed;
                verifiedSubscription?.Dispose();
                if (!_authCodeWindow.IsVerified)
                {
                    // 未验证成功就关闭，返回false
                    tcs.TrySetResult(false);
                }
            }
        }

        _authCodeWindow.Closed += OnWindowClosed;

        return await tcs.Task;
    }

    private async Task<bool> ShowLoginWindowAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        if (_loginWindow == null)
        {
            return false;
        }

        // 显示 LoginWindow，隐藏 AttendedWeighingWindow
        _loginWindow.Show();
        if (_attendedWeighingWindow != null)
        {
            _attendedWeighingWindow.Hide();
        }

        // AuthCodeWindow 应该已经隐藏，但为了安全也隐藏它
        if (_authCodeWindow != null && _authCodeWindow.IsVisible)
        {
            _authCodeWindow.Hide();
        }

        IDisposable? loginSuccessSubscription = null;

        // 监听登录成功事件（通过ViewModel的IsLoginSuccessful属性变化）
        if (_loginWindow.DataContext is LoginWindowViewModel viewModel)
        {
            loginSuccessSubscription = viewModel
                .WhenAnyValue(vm => vm.IsLoginSuccessful)
                .Where(isSuccessful => isSuccessful)
                .Subscribe(_ =>
                {
                    // 登录成功，隐藏LoginWindow，显示AttendedWeighingWindow
                    _loginWindow?.Hide();
                    if (_attendedWeighingWindow != null)
                    {
                        _attendedWeighingWindow.Show();
                    }

                    loginSuccessSubscription?.Dispose();
                    tcs.TrySetResult(true);
                });
        }

        // 监听窗口关闭事件（用户点击关闭按钮）
        void OnWindowClosed(object? sender, EventArgs args)
        {
            if (_loginWindow != null)
            {
                _loginWindow.Closed -= OnWindowClosed;
                loginSuccessSubscription?.Dispose();
                if (!_loginWindow.IsLoginSuccessful)
                {
                    // 未登录成功就关闭，返回false
                    tcs.TrySetResult(false);
                }
            }
        }

        _loginWindow.Closed += OnWindowClosed;

        return await tcs.Task;
    }

    private void ShowAttendedWeighingWindow()
    {
        if (_attendedWeighingWindow == null)
        {
            return;
        }

        // 显示 AttendedWeighingWindow，隐藏其他窗口
        _attendedWeighingWindow.Show();
        if (_authCodeWindow != null && _authCodeWindow.IsVisible)
        {
            _authCodeWindow.Hide();
        }

        if (_loginWindow != null && _loginWindow.IsVisible)
        {
            _loginWindow.Hide();
        }
    }
}