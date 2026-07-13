using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.UI.ViewModels;
using MaterialClient.UI.Views;
using MaterialClient.Views.AttendedWeighing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Recycle.Services;

/// <summary>
///     Recycle 应用启动服务：授权码（5020）→ MaterialPlatform 登录 → Attended 称重主界面。
/// </summary>
public class RecycleStartupService(
    ILicenseService licenseService,
    IAuthenticationService authenticationService,
    IServiceProvider serviceProvider,
    ILogger<RecycleStartupService>? logger = null) : ITransientDependency
{
    private AuthCodeWindow? _authCodeWindow;
    private LoginWindow? _loginWindow;
    private AttendedWeighingWindow? _attendedWeighingWindow;

    public async Task<AttendedWeighingWindow?> StartupAsync()
    {
        try
        {
            await SyncAutoStartOnStartupAsync();

            _authCodeWindow = serviceProvider.GetRequiredService<AuthCodeWindow>();
            _loginWindow = serviceProvider.GetRequiredService<LoginWindow>();
            _attendedWeighingWindow = serviceProvider.GetRequiredService<AttendedWeighingWindow>();

            _authCodeWindow.Hide();
            _loginWindow.Hide();
            _attendedWeighingWindow.Hide();

            var isLicenseValid = await licenseService.IsLicenseValidAsync();
            var licenseWasInvalid = !isLicenseValid;

            if (!isLicenseValid)
            {
                var authResult = await ShowAuthorizationWindowAsync();
                if (!authResult)
                {
                    return null;
                }
            }

            if (licenseWasInvalid)
            {
                await authenticationService.LogoutAsync();
            }

            if (!await authenticationService.HasActiveSessionAsync())
            {
                var loginResult = await ShowLoginWindowAsync();
                if (!loginResult)
                {
                    return null;
                }
            }

            ShowAttendedWeighingWindow();
            return _attendedWeighingWindow;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Recycle 启动失败");
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

        _authCodeWindow.Show();
        _loginWindow?.Hide();
        _attendedWeighingWindow?.Hide();

        IDisposable? verifiedSubscription = null;

        if (_authCodeWindow.DataContext is AuthCodeWindowViewModel viewModel)
        {
            verifiedSubscription = viewModel
                .WhenAnyValue(vm => vm.IsVerified)
                .Where(isVerified => isVerified)
                .Subscribe(_ =>
                {
                    _authCodeWindow?.Hide();
                    _loginWindow?.Show();
                    verifiedSubscription?.Dispose();
                    tcs.TrySetResult(true);
                });
        }

        void OnWindowClosed(object? sender, EventArgs args)
        {
            if (_authCodeWindow != null)
            {
                _authCodeWindow.Closed -= OnWindowClosed;
                verifiedSubscription?.Dispose();
                if (!_authCodeWindow.IsVerified)
                {
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

        _loginWindow.Show();
        _attendedWeighingWindow?.Hide();
        if (_authCodeWindow is { IsVisible: true })
        {
            _authCodeWindow.Hide();
        }

        IDisposable? loginSuccessSubscription = null;

        if (_loginWindow.DataContext is LoginWindowViewModel viewModel)
        {
            loginSuccessSubscription = viewModel
                .WhenAnyValue(vm => vm.IsLoginSuccessful)
                .Where(isSuccessful => isSuccessful)
                .Subscribe(_ =>
                {
                    _loginWindow?.Hide();
                    _attendedWeighingWindow?.Show();
                    loginSuccessSubscription?.Dispose();
                    tcs.TrySetResult(true);
                });
        }

        void OnWindowClosed(object? sender, EventArgs args)
        {
            if (_loginWindow != null)
            {
                _loginWindow.Closed -= OnWindowClosed;
                loginSuccessSubscription?.Dispose();
                if (!_loginWindow.IsLoginSuccessful)
                {
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

        _attendedWeighingWindow.Show();
        if (_authCodeWindow is { IsVisible: true })
        {
            _authCodeWindow.Hide();
        }

        if (_loginWindow is { IsVisible: true })
        {
            _loginWindow.Hide();
        }
    }

    private async Task SyncAutoStartOnStartupAsync()
    {
        try
        {
            var settingsService = serviceProvider.GetService<ISettingsService>();
            var autoStartService = serviceProvider.GetService<IWindowsAutoStartService>();
            if (settingsService == null || autoStartService == null)
            {
                return;
            }

            var settings = await settingsService.GetSettingsAsync();
            var dbAutoStartEnabled = settings.SystemSettings.EnableAutoStart;
            var registryAutoStartEnabled = await autoStartService.IsAutoStartEnabledAsync();

            if (dbAutoStartEnabled != registryAutoStartEnabled)
            {
                if (dbAutoStartEnabled)
                {
                    await autoStartService.EnableAutoStartAsync();
                }
                else
                {
                    await autoStartService.DisableAutoStartAsync();
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Recycle 自启动同步失败，继续启动。");
        }
    }
}
