using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Recycle.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ursa.Controls;
using Volo.Abp;

namespace MaterialClient.Recycle;

public class App : Application
{
    private IAbpApplicationWithInternalServiceProvider? _abpApplication;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 未授权流程中会临时切换 MainWindow；显式退出，避免关闭提示窗时整个进程被关掉
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                // Create and initialize ABP application with Autofac (matching MaterialClient/Urban pattern)
                _abpApplication = await AbpApplicationFactory.CreateAsync<MaterialClientRecycleModule>(options =>
                {
                    options.UseAutofac();
                });

                await _abpApplication.InitializeAsync();

                // Register exit cleanup before any Shutdown path.
                desktop.Exit += OnApplicationExit;

                var logger = _abpApplication.ServiceProvider.GetService<ILogger<App>>();

                // Recycle 授权沿用 SolidWaste（5010）的非 JWT 模式：
                // 仅校验本地 LicenseInfo 是否存在且未过期（IsLicenseValidAsync 不依赖 JWT）。
                var licenseService = _abpApplication.ServiceProvider.GetRequiredService<ILicenseService>();
                var isAuthorized = await licenseService.IsLicenseValidAsync();

                if (!isAuthorized)
                {
                    logger?.LogWarning("Recycle 启动授权检查失败：本地无有效授权（LicenseInfo 缺失或已过期）。");
                    await HandleUnauthorizedStartupAsync(desktop);
                    desktop.Shutdown();
                    return;
                }

                logger?.LogInformation("Recycle 启动授权检查通过，显示主窗口并初始化称重管线。");

                // Resolve main window from ABP container and show it.
                var window = _abpApplication.ServiceProvider.GetRequiredService<RecycleMainWindow>();
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                desktop.MainWindow = window;
                window.Show();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Recycle] ABP initialization error: {ex.Message}");
                desktop.Shutdown();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    ///     未授权启动处理：显示「软件未授权」提示对话框，用户确认后退出应用。
    /// </summary>
    private async Task HandleUnauthorizedStartupAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            // 临时挂一个不可见的主窗口，供 MessageBox 作为父级宿主。
            var host = new Window { Width = 0, Height = 0, ShowInTaskbar = false, SystemDecorations = SystemDecorations.None };
            desktop.MainWindow = host;
            host.Show();
            await MessageBox.ShowAsync(
                host,
                "软件未授权，请联系管理员获取授权后重启软件。",
                "提示",
                MessageBoxIcon.Warning,
                MessageBoxButton.OK);
            host.Close();
        }
        catch (Exception ex)
        {
            var logger = _abpApplication?.ServiceProvider.GetService<ILogger<App>>();
            logger?.LogError(ex, "Failed to show unauthorized startup dialog.");
        }
    }

    private async void OnApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        var logger = _abpApplication?.ServiceProvider.GetService<ILogger<App>>();
        logger?.LogInformation("Recycle application exit triggered, starting cleanup...");
        var totalSw = Stopwatch.StartNew();

        try
        {
            var shutdownTask = Task.Run(async () =>
            {
                if (_abpApplication != null)
                {
                    var sw = Stopwatch.StartNew();
                    logger?.LogInformation("Shutting down ABP application...");
                    try
                    {
                        await _abpApplication.ShutdownAsync();
                        _abpApplication.Dispose();
                        _abpApplication = null;
                        logger?.LogInformation("ABP application shut down ({ElapsedMs}ms)", sw.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Error shutting down ABP application");
                    }
                }

                logger?.LogInformation("Resource cleanup completed (total {TotalMs}ms)", totalSw.ElapsedMilliseconds);
            });

            if (!shutdownTask.Wait(TimeSpan.FromSeconds(10)))
            {
                logger?.LogWarning("Resource cleanup timed out (10s), forcing exit");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during application exit, forcing exit");
        }
    }
}
