using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MaterialClient.Common.Events;
using MaterialClient.Urban.Services;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace MaterialClient.Urban.Events;

/// <summary>
///     Handles <see cref="LicenseExpiredEto" />: authorization has expired on the server.
///     Hides the main weighing window, shows the activation window, and restarts on success.
/// </summary>
[AutoConstructor]
public partial class LicenseExpiredEventHandler
    : ILocalEventHandler<LicenseExpiredEto>, ITransientDependency
{
    private const string DefaultExpiredMessage = "授权已过期";

    private readonly ILogger<LicenseExpiredEventHandler> _logger;
    private readonly IUrbanLicenseRecoveryService _recoveryService;

    public async Task HandleEventAsync(LicenseExpiredEto eventData)
    {
#if DEBUG
        _logger.LogWarning(
            "DEBUG Urban authorization bypass active: ignoring LicenseExpiredEto. ProjectId={ProjectId}",
            eventData.ProjectId);
        await Task.CompletedTask;
#else
        var message = string.IsNullOrWhiteSpace(eventData.Reason)
            ? DefaultExpiredMessage
            : eventData.Reason;

        _logger.LogWarning(
            "License expired. ProjectId={ProjectId}, Reason={Reason}. Starting recovery.",
            eventData.ProjectId,
            message);

        var activated = await Dispatcher.UIThread.InvokeAsync(
            async () => await _recoveryService.RecoverAsync(message));

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (activated)
        {
            _logger.LogInformation(
                "Re-activation successful after license expiry. ProjectId={ProjectId}. Requesting process restart.",
                eventData.ProjectId);

            App.RequestProcessRestart();
            desktop.Shutdown();
            return;
        }

        desktop.Shutdown();
#endif
    }
}
