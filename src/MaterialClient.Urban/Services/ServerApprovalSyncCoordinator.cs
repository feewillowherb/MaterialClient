using MaterialClient.Common.Models;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Urban.Api;
using MaterialClient.Urban.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Urban.Services;

/// <summary>
///     Subscribes to SignalR server-approval push and pulls pending sync on reconnect.
/// </summary>
public class ServerApprovalSyncCoordinator : ISingletonDependency, IAsyncDisposable
{
    private readonly ILogger<ServerApprovalSyncCoordinator> _logger;
    private readonly IDeviceStatusSignalRClient? _signalRClient;
    private readonly IServerApprovalSyncService _serverApprovalSyncService;
    private readonly IUrbanManagementApi _urbanManagementApi;
    private readonly ILicenseService _licenseService;
    private readonly ILocalEventBus _localEventBus;

    private IDisposable? _connectionRestoredSubscription;
    private bool _initialized;

    public ServerApprovalSyncCoordinator(
        ILogger<ServerApprovalSyncCoordinator> logger,
        IServerApprovalSyncService serverApprovalSyncService,
        IUrbanManagementApi urbanManagementApi,
        ILicenseService licenseService,
        ILocalEventBus localEventBus,
        IDeviceStatusSignalRClient? signalRClient = null)
    {
        _logger = logger;
        _serverApprovalSyncService = serverApprovalSyncService;
        _urbanManagementApi = urbanManagementApi;
        _licenseService = licenseService;
        _localEventBus = localEventBus;
        _signalRClient = signalRClient;
    }

    public Task InitializeAsync()
    {
        if (_initialized)
        {
            return Task.CompletedTask;
        }

        if (_signalRClient == null)
        {
            _logger.LogWarning("SignalR client not available; server approval sync coordinator disabled.");
            return Task.CompletedTask;
        }

        _signalRClient.OnWeighingRecordApproved(HandlePushAsync);

        _connectionRestoredSubscription?.Dispose();
        _connectionRestoredSubscription = _localEventBus.Subscribe<SignalRConnectionRestoredEventData>(eventData =>
        {
            _ = PullPendingAsync();
            return Task.CompletedTask;
        });

        _initialized = true;
        _logger.LogInformation("ServerApprovalSyncCoordinator initialized.");
        _ = PullPendingAsync();
        return Task.CompletedTask;
    }

    private async Task HandlePushAsync(WeighingRecordApprovedPushDto push)
    {
        try
        {
            await _serverApprovalSyncService.ApplyServerApprovalAsync(push);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to apply server approval push for ClientRecordId={ClientRecordId}",
                push.ClientRecordId);
        }
    }

    public async Task PullPendingAsync()
    {
        try
        {
            var license = await _licenseService.GetCurrentLicenseAsync();
            if (license == null || license.ProjectId == Guid.Empty)
            {
                return;
            }

            var pending = await _urbanManagementApi.GetPendingServerApprovalSyncAsync(
                new PendingServerApprovalSyncQueryDto { ProId = license.ProjectId });

            foreach (var push in pending)
            {
                await _serverApprovalSyncService.ApplyServerApprovalAsync(push);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to pull pending server approval sync records.");
        }
    }

    public ValueTask DisposeAsync()
    {
        _connectionRestoredSubscription?.Dispose();
        return ValueTask.CompletedTask;
    }
}
