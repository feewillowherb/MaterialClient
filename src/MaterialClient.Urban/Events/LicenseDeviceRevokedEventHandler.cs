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
///     Handles <see cref="LicenseDeviceRevokedEto" /> (F4): the server determined the
///     authorization device has changed. Forces the online-only activation window; if the user
///     cancels, the application is shut down gracefully (no crash).
/// </summary>
[AutoConstructor]
public partial class LicenseDeviceRevokedEventHandler
    : ILocalEventHandler<LicenseDeviceRevokedEto>, ITransientDependency
{
    private readonly ILogger<LicenseDeviceRevokedEventHandler> _logger;
    private readonly IUrbanLicenseRecoveryService _recoveryService;

    public async Task HandleEventAsync(LicenseDeviceRevokedEto eventData)
    {
#if DEBUG
        _logger.LogWarning(
            "DEBUG Urban authorization bypass active: ignoring LicenseDeviceRevokedEto. ProjectId={ProjectId}",
            eventData.ProjectId);
        await Task.CompletedTask;
#else
        _logger.LogWarning(
            "License device revoked by server. ProjectId={ProjectId}, Reason={Reason}. " +
            "Forcing online re-activation.",
            eventData.ProjectId, eventData.Reason);

        var activated = await Dispatcher.UIThread.InvokeAsync(
            async () => await _recoveryService.RecoverAsync(eventData.Reason));

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (activated)
        {
            _logger.LogInformation(
                "Re-activation successful after device revocation. ProjectId={ProjectId}. Requesting process restart.",
                eventData.ProjectId);

            App.RequestProcessRestart();
            desktop.Shutdown();
            return;
        }

        // User chose to exit (did not re-activate) → shut the application down gracefully.
        desktop.Shutdown();
#endif
    }
}
