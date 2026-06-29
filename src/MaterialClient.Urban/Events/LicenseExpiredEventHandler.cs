using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Urban.Views.Dialogs;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace MaterialClient.Urban.Events;

/// <summary>
///     Handles <see cref="LicenseExpiredEto" />: authorization has expired on the server.
///     Shows a notice and shuts down the application (no online re-activation loop).
/// </summary>
[AutoConstructor]
public partial class LicenseExpiredEventHandler
    : ILocalEventHandler<LicenseExpiredEto>, ITransientDependency
{
    private const string DefaultExpiredMessage = "授权已过期";

    private readonly ILogger<LicenseExpiredEventHandler> _logger;
    private readonly IMachineCodeService _machineCodeService;

    public async Task HandleEventAsync(LicenseExpiredEto eventData)
    {
        var message = string.IsNullOrWhiteSpace(eventData.Reason)
            ? DefaultExpiredMessage
            : eventData.Reason;

        _logger.LogWarning(
            "License expired. ProjectId={ProjectId}, Reason={Reason}. Shutting down application.",
            eventData.ProjectId,
            message);

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return;
            }

            var notice = new UnauthorizedNoticeWindow(message, _machineCodeService.GetMachineCode());
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

            var closedTcs = new TaskCompletionSource();
            notice.Closed += (_, _) => closedTcs.TrySetResult();
            notice.Show();

            await closedTcs.Task;

            if (!isStartup && blockingOwner != null)
            {
                blockingOwner.IsEnabled = true;
            }

            desktop.Shutdown();
        });
    }
}
