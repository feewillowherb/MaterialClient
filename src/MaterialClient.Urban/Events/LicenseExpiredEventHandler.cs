using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Urban.ViewModels;
using MaterialClient.Urban.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace MaterialClient.Urban.Events;

/// <summary>
///     Handles <see cref="LicenseExpiredEto" />: authorization has expired on the server.
///     Shows the activation window so the user can re-activate, or shuts down if the user
///     cancels.
/// </summary>
[AutoConstructor]
public partial class LicenseExpiredEventHandler
    : ILocalEventHandler<LicenseExpiredEto>, ITransientDependency
{
    private const string DefaultExpiredMessage = "授权已过期";

    private readonly ILogger<LicenseExpiredEventHandler> _logger;
    private readonly IMachineCodeService _machineCodeService;
    private readonly IServiceProvider _serviceProvider;

    public async Task HandleEventAsync(LicenseExpiredEto eventData)
    {
        var message = string.IsNullOrWhiteSpace(eventData.Reason)
            ? DefaultExpiredMessage
            : eventData.Reason;

        _logger.LogWarning(
            "License expired. ProjectId={ProjectId}, Reason={Reason}. Showing activation window.",
            eventData.ProjectId,
            message);

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return;
            }

            var activationWindow = _serviceProvider.GetRequiredService<UrbanActivationWindow>();
            var viewModel = (UrbanActivationWindowViewModel)activationWindow.DataContext!;
            viewModel.FailureReason = message;

            var blockingOwner = desktop.MainWindow;
            var isStartup = blockingOwner == null;

            if (isStartup)
            {
                desktop.MainWindow = activationWindow;
                activationWindow.Show();

                var closedTcs = new TaskCompletionSource<bool>();
                activationWindow.Closed += (_, _) => closedTcs.TrySetResult(activationWindow.ActivationResult);
                var result = await closedTcs.Task;

                if (!result)
                {
                    desktop.Shutdown();
                }
            }
            else
            {
                blockingOwner!.IsEnabled = false;
                try
                {
                    var result = await activationWindow.ShowDialog<bool?>(blockingOwner);
                    if (result != true)
                    {
                        desktop.Shutdown();
                    }
                }
                finally
                {
                    blockingOwner.IsEnabled = true;
                }
            }
        });
    }
}
