using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Urban.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Services;

/// <summary>
///     Shared online-activation recovery flow used by both the startup authorization gate
///     and the runtime device-revocation path (F4). Shows the online-only
///     <see cref="UnauthorizedNoticeWindow" /> + activation loop; returns <c>true</c> when the
///     user activates successfully, or <c>false</c> when the user chooses to exit (the caller
///     is then responsible for shutting the application down).
/// </summary>
public interface IUrbanLicenseRecoveryService
{
    Task<bool> RecoverAsync(string failureMessage);
}

/// <inheritdoc />
[AutoConstructor]
public partial class UrbanLicenseRecoveryService : IUrbanLicenseRecoveryService, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILicenseService _licenseService;
    private readonly IMachineCodeService _machineCodeService;

    public async Task<bool> RecoverAsync(string failureMessage)
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktop == null)
        {
            return false;
        }

        var machineCode = _machineCodeService.GetMachineCode();
        var notice = new UnauthorizedNoticeWindow(failureMessage, machineCode);

        // Startup path: no main window yet → notice becomes the main window.
        // Runtime path (device revoked): the weighing window already exists → block it so the
        // user cannot continue weighing business while recovery is pending.
        var blockingOwner = desktop.MainWindow;
        var isStartup = blockingOwner == null;
        if (isStartup)
        {
            desktop.MainWindow = notice;
        }
        else
        {
            blockingOwner!.IsEnabled = false;
        }

        notice.Show();

        try
        {
            while (true)
            {
                var activationWindow = _serviceProvider.GetRequiredService<UrbanActivationWindow>();
                var activated = await activationWindow.ShowDialog<bool?>(notice);
                if (activated == true)
                {
                    if (await _licenseService.IsLicenseValidAsync())
                    {
                        return true;
                    }

                    continue;
                }

                var userChoice = await AwaitNoticeChoiceAsync(notice);

                if (userChoice == UnauthorizedNoticeWindow.UnauthorizedNoticeResult.Exit)
                {
                    return false;
                }
            }
        }
        finally
        {
            if (notice.IsVisible)
            {
                notice.Close();
            }

            if (!isStartup && blockingOwner != null)
            {
                blockingOwner.IsEnabled = true;
            }
        }
    }

    private static Task<UnauthorizedNoticeWindow.UnauthorizedNoticeResult> AwaitNoticeChoiceAsync(
        UnauthorizedNoticeWindow notice)
    {
        var choiceTcs = new TaskCompletionSource<UnauthorizedNoticeWindow.UnauthorizedNoticeResult>();

        void OnOnlineActivateRequested(object? sender, EventArgs e)
        {
            Cleanup();
            choiceTcs.TrySetResult(UnauthorizedNoticeWindow.UnauthorizedNoticeResult.OnlineActivate);
        }

        void OnNoticeClosed(object? sender, EventArgs e)
        {
            if (choiceTcs.Task.IsCompleted)
            {
                return;
            }

            Cleanup();
            choiceTcs.TrySetResult(notice.UserChoice);
        }

        void Cleanup()
        {
            notice.OnlineActivateRequested -= OnOnlineActivateRequested;
            notice.Closed -= OnNoticeClosed;
        }

        notice.OnlineActivateRequested += OnOnlineActivateRequested;
        notice.Closed += OnNoticeClosed;
        return choiceTcs.Task;
    }
}
