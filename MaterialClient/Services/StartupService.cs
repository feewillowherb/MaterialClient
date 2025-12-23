using System;
using System.Threading.Tasks;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Views.AttendedWeighing;
using MaterialClient.ViewModels;
using MaterialClient.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    /// <summary>
    /// 执行启动流程
    /// 1. 检查授权状态
    /// 2. 如果无授权或授权过期，显示授权窗口
    /// 3. 显示登录窗口
    /// 4. 成功登录后显示有人值守过磅窗口
    /// </summary>
    public async Task<AttendedWeighingWindow?> StartupAsync()
    {
        try
        {
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
            // Resolve Window from Autofac container (ViewModel is injected via constructor)
            var attendedWeighingWindow = serviceProvider.GetRequiredService<AttendedWeighingWindow>();

            return attendedWeighingWindow;
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

        // Resolve Window from Autofac container (ViewModel is injected via constructor)
        var authWindow = serviceProvider.GetRequiredService<AuthCodeWindow>();

        authWindow.Closed += (sender, args) => { tcs.SetResult(authWindow.IsVerified); };

        authWindow.Show();

        return await tcs.Task;
    }

    private async Task<bool> ShowLoginWindowAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        // Resolve Window from Autofac container (ViewModel is injected via constructor)
        var loginWindow = serviceProvider.GetRequiredService<LoginWindow>();

        loginWindow.Closed += (sender, args) => { tcs.SetResult(loginWindow.IsLoginSuccessful); };

        loginWindow.Show();

        return await tcs.Task;
    }
}