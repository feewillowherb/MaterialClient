using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Urban.ViewModels;
using MaterialClient.Urban.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Services;

/// <summary>
///     Shared online-activation recovery flow used by the startup authorization gate,
///     runtime license expiry, and device-revocation path (F4). Hides the main weighing
///     window when present, shows <see cref="UrbanActivationWindow" /> as MainWindow;
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

        var blockingOwner = desktop.MainWindow;
        var isStartup = blockingOwner == null;

        while (true)
        {
            var activationWindow = _serviceProvider.GetRequiredService<UrbanActivationWindow>();
            var viewModel = (UrbanActivationWindowViewModel)activationWindow.DataContext!;
            viewModel.FailureReason = failureMessage;

            bool? result;

            if (isStartup)
            {
                desktop.MainWindow = activationWindow;
                activationWindow.Show();
                result = await AwaitWindowResultAsync(activationWindow);
            }
            else
            {
                blockingOwner.Hide();
                desktop.MainWindow = activationWindow;
                activationWindow.Show();
                result = await AwaitWindowResultAsync(activationWindow);
            }

            if (result == true)
            {
                if (await _licenseService.IsLicenseValidAsync())
                {
                    return true;
                }

                failureMessage = "激活完成但授权验证未通过，请重新激活";
                continue;
            }

            return false;
        }
    }

    private static async Task<bool?> AwaitWindowResultAsync(UrbanActivationWindow window)
    {
        var tcs = new TaskCompletionSource<bool?>();
        window.Closed += (_, _) =>
        {
            tcs.TrySetResult(window.ActivationResult);
        };
        return await tcs.Task;
    }
}
