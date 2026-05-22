using MaterialClient.Common.Services.Authentication;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace MaterialClient.Common.Events;

/// <summary>
///     Handles SessionRefreshRequiredEto events by attempting to re-login
///     using saved credentials.
/// </summary>
[AutoConstructor]
public partial class SessionRefreshRequiredEventHandler : ILocalEventHandler<SessionRefreshRequiredEto>,
    ITransientDependency
{
    private readonly ILogger<SessionRefreshRequiredEventHandler> _logger;
    private readonly IAuthenticationService _authenticationService;

    public async Task HandleEventAsync(SessionRefreshRequiredEto eventData)
    {
        _logger.LogInformation(
            "Received session refresh event for endpoint {Endpoint} (status {StatusCode})",
            eventData.ApiEndpoint, eventData.StatusCode);

        try
        {
            var credential = await _authenticationService.GetSavedCredentialAsync();
            if (credential.HasValue)
            {
                _logger.LogInformation("Token expired, attempting re-login...");
                await _authenticationService.LoginAsync(
                    credential.Value.username,
                    credential.Value.password,
                    rememberMe: true);
                _logger.LogInformation("Re-login successful, session refreshed.");
            }
            else
            {
                _logger.LogWarning("Token expired but no saved credentials found, cannot re-login.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Re-login failed after token expiration.");
        }
    }
}
