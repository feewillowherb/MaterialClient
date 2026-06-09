using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services.Authentication;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     SignalR client for device status upload.
///     Manages connection lifecycle, automatic reconnection, and message queuing.
/// </summary>
public interface IDeviceStatusSignalRClient : ISingletonDependency
{
    /// <summary>
    ///     Starts the SignalR connection to the server.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stops the SignalR connection and cancels reconnection attempts.
    /// </summary>
    Task StopAsync();

    /// <summary>
    ///     Uploads a device status message to the server.
    ///     If disconnected, queues the message for later delivery.
    /// </summary>
    Task UploadStatusAsync(DeviceStatusMessage message);

    /// <summary>
    ///     Current connection state.
    /// </summary>
    HubConnectionState ConnectionState { get; }
}

/// <inheritdoc />
public class DeviceStatusSignalRClient : IDeviceStatusSignalRClient, IAsyncDisposable
{
    private readonly ILogger<DeviceStatusSignalRClient> _logger;
    private readonly ILicenseService _licenseService;
    private readonly SignalRClientOptions _options;

    private HubConnection? _connection;
    private readonly ConcurrentQueue<DeviceStatusMessage> _messageQueue = new();
    private CancellationTokenSource? _reconnectCts;
    private int _reconnectAttempts;
    private bool _isStarted;
    private bool _isStopped;
    private readonly object _lock = new();

    public DeviceStatusSignalRClient(
        ILogger<DeviceStatusSignalRClient> logger,
        ILicenseService licenseService,
        IOptions<SignalRClientOptions> options)
    {
        _logger = logger;
        _licenseService = licenseService;
        _options = options.Value;

        // Validate and clamp configuration
        if (_options.MessageQueueSize is <= 0 or > 1000)
        {
            _options.MessageQueueSize = 100;
        }
    }

    /// <inheritdoc />
    public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isStarted) return;

        // Validate configuration
        if (string.IsNullOrWhiteSpace(_options.ServerUrl))
        {
            throw new InvalidOperationException(
                "SignalR server URL is not configured. Please set 'SignalR:ServerUrl' in appsettings.json.");
        }

        if (!Uri.TryCreate(_options.ServerUrl, UriKind.Absolute, out var serverUri))
        {
            throw new InvalidOperationException(
                $"Invalid SignalR server URL: '{_options.ServerUrl}'. Please check 'SignalR:ServerUrl' in appsettings.json.");
        }

        _logger.LogInformation(
            "DeviceStatusSignalRClient: Starting connection to {ServerUrl}",
            _options.ServerUrl);

        _connection = new HubConnectionBuilder()
            .WithUrl(serverUri, options =>
            {
                // Configure JWT token provider if token is available
                if (!string.IsNullOrWhiteSpace(_options.AccessToken))
                {
                    options.AccessTokenProvider = () => Task.FromResult(_options.AccessToken);
                }
            })
            .WithAutomaticReconnect(_options.ReconnectDelays.Select(d => TimeSpan.FromSeconds(d)).ToArray())
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .Build();

        // Subscribe to connection lifecycle events
        _connection.Closed += OnConnectionClosed;
        _connection.Reconnecting += OnConnectionReconnecting;
        _connection.Reconnected += OnConnectionReconnected;

        // Subscribe to server-side HelloResponse for testing
        _connection.On<string>("HelloResponse", response =>
        {
            _logger.LogInformation(
                "DeviceStatusSignalRClient: Received HelloResponse: {Response}",
                response);
        });

        // Subscribe to DeviceStatusUpdate from server (for future bidirectional communication)
        _connection.On<DeviceStatusMessage>("DeviceStatusUpdate", message =>
        {
            _logger.LogDebug(
                "DeviceStatusSignalRClient: Received DeviceStatusUpdate from server. ClientId={ClientId}, DeviceType={DeviceType}, Status={Status}",
                message.ClientId, message.DeviceType, message.Status);
        });

        try
        {
            await _connection.StartAsync(cancellationToken);
            _isStarted = true;
            _logger.LogInformation(
                "DeviceStatusSignalRClient: Connected successfully. ConnectionId={ConnectionId}",
                _connection.ConnectionId);

            await SyncProjectLicenseFromServerAsync();
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex,
                "DeviceStatusSignalRClient: Connection timeout to {ServerUrl}",
                _options.ServerUrl);
            StartReconnectLoop();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "DeviceStatusSignalRClient: Failed to connect to {ServerUrl}",
                _options.ServerUrl);
            StartReconnectLoop();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        _logger.LogInformation("DeviceStatusSignalRClient: Stopping connection...");

        _isStopped = true;

        // Cancel reconnection attempts
        _reconnectCts?.Cancel();

        if (_connection != null)
        {
            try
            {
                await _connection.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DeviceStatusSignalRClient: Error stopping connection.");
            }
        }

        _isStarted = false;
        _logger.LogInformation("DeviceStatusSignalRClient: Connection stopped.");
    }

    /// <inheritdoc />
    public async Task UploadStatusAsync(DeviceStatusMessage message)
    {
        if (_connection == null || !_isStarted)
        {
            _logger.LogWarning(
                "DeviceStatusSignalRClient: Cannot send message, connection not started. Queuing message for DeviceType={DeviceType}",
                message.DeviceType);
            EnqueueMessage(message);
            return;
        }

        if (_connection.State == HubConnectionState.Connected)
        {
            try
            {
                await _connection.SendAsync("UploadStatus", message);
                _logger.LogDebug(
                    "DeviceStatusSignalRClient: Sent status for DeviceType={DeviceType}, Status={Status}",
                    message.DeviceType, message.Status);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "DeviceStatusSignalRClient: Failed to send status for DeviceType={DeviceType}. Queuing message.",
                    message.DeviceType);
                EnqueueMessage(message);
            }
        }
        else
        {
            _logger.LogWarning(
                "DeviceStatusSignalRClient: Connection state is {State}. Queuing message for DeviceType={DeviceType}",
                _connection.State, message.DeviceType);
            EnqueueMessage(message);
        }
    }

    /// <summary>
    ///     Enqueues a message for later delivery. Maintains max queue size (FIFO).
    /// </summary>
    private void EnqueueMessage(DeviceStatusMessage message)
    {
        _messageQueue.Enqueue(message);

        // Enforce FIFO - remove oldest if over limit
        while (_messageQueue.Count > _options.MessageQueueSize)
        {
            _messageQueue.TryDequeue(out _);
        }

        if (_messageQueue.Count > _options.MessageQueueSize * 0.8)
        {
            _logger.LogWarning(
                "DeviceStatusSignalRClient: Message queue is {Count}/{Max} capacity",
                _messageQueue.Count, _options.MessageQueueSize);
        }
    }

    /// <summary>
    ///     Sends all queued messages after reconnection.
    /// </summary>
    private async Task FlushMessageQueueAsync()
    {
        if (_connection?.State != HubConnectionState.Connected) return;

        var flushedCount = 0;
        while (_messageQueue.TryDequeue(out var message))
        {
            try
            {
                await _connection.SendAsync("UploadStatus", message);
                flushedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "DeviceStatusSignalRClient: Failed to flush queued message for DeviceType={DeviceType}",
                    message.DeviceType);
                // Re-enqueue at front
                EnqueueMessage(message);
                break;
            }
        }

        if (flushedCount > 0)
        {
            _logger.LogInformation(
                "DeviceStatusSignalRClient: Flushed {Count} queued messages after reconnection.",
                flushedCount);
        }
    }

    /// <summary>
    ///     Starts the reconnection loop with exponential backoff.
    /// </summary>
    private void StartReconnectLoop()
    {
        lock (_lock)
        {
            if (_isStopped) return;
            if (_reconnectCts != null && !_reconnectCts.IsCancellationRequested) return;

            _reconnectCts = new CancellationTokenSource();
        }

        _ = ReconnectLoopAsync(_reconnectCts.Token);
    }

    /// <summary>
    ///     Reconnection loop with configurable delays and max attempts.
    /// </summary>
    private async Task ReconnectLoopAsync(CancellationToken cancellationToken)
    {
        _reconnectAttempts = 0;
        var delays = _options.ReconnectDelays;
        var maxAttempts = _options.MaxReconnectAttempts;

        while (!cancellationToken.IsCancellationRequested && _reconnectAttempts < maxAttempts)
        {
            _reconnectAttempts++;

            // Calculate delay: use configured delays, then cap at 60 seconds
            var delaySeconds = _reconnectAttempts <= delays.Length
                ? delays[_reconnectAttempts - 1]
                : Math.Min((int)Math.Pow(2, _reconnectAttempts), 60);

            _logger.LogDebug(
                "DeviceStatusSignalRClient: Reconnect attempt {Attempt}/{Max} in {Delay}s",
                _reconnectAttempts, maxAttempts, delaySeconds);

            if (delaySeconds > 0)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                if (_connection != null)
                {
                    await _connection.StartAsync(cancellationToken);
                    _logger.LogInformation(
                        "DeviceStatusSignalRClient: Reconnected successfully on attempt {Attempt}.",
                        _reconnectAttempts);
                    _reconnectAttempts = 0;

                    await FlushMessageQueueAsync();
                    await SyncProjectLicenseFromServerAsync();
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "DeviceStatusSignalRClient: Reconnect attempt {Attempt} failed.",
                    _reconnectAttempts);
            }
        }

        if (_reconnectAttempts >= maxAttempts)
        {
            _logger.LogError(
                "DeviceStatusSignalRClient: Max reconnect attempts ({Max}) reached. Giving up.",
                maxAttempts);
        }
    }

    private Task OnConnectionClosed(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception,
                "DeviceStatusSignalRClient: Connection closed with error.");
        }
        else
        {
            _logger.LogInformation("DeviceStatusSignalRClient: Connection closed.");
        }

        if (!_isStopped)
        {
            StartReconnectLoop();
        }

        return Task.CompletedTask;
    }

    private Task OnConnectionReconnecting(Exception? exception)
    {
        _logger.LogInformation(
            "DeviceStatusSignalRClient: Connection reconnecting... Reason: {Reason}",
            exception?.Message ?? "unknown");

        return Task.CompletedTask;
    }

    private async Task OnConnectionReconnected(string? connectionId)
    {
        _logger.LogInformation(
            "DeviceStatusSignalRClient: Reconnected. New ConnectionId={ConnectionId}",
            connectionId);

        _reconnectAttempts = 0;

        await FlushMessageQueueAsync();
        await SyncProjectLicenseFromServerAsync();
    }

    private async Task SyncProjectLicenseFromServerAsync()
    {
        if (_connection?.State != HubConnectionState.Connected)
        {
            return;
        }

        try
        {
            var license = await _licenseService.GetCurrentLicenseAsync();
            if (license == null)
            {
                _logger.LogDebug(
                    "DeviceStatusSignalRClient: Skip project license sync because no local license exists.");
                return;
            }

            var projectInfo = await _connection.InvokeAsync<ClientProjectLicenseInfoDto?>(
                "GetClientProjectLicenseInfo",
                license.ProjectId.ToString());

            if (projectInfo == null)
            {
                _logger.LogDebug(
                    "DeviceStatusSignalRClient: Server returned no project license info for ProId={ProId}",
                    license.ProjectId);
                return;
            }

            var updated = await _licenseService.SyncProjectFieldsFromServerAsync(
                projectInfo.ProName,
                projectInfo.BuildLicenseNo,
                projectInfo.FdBuildLicenseNo,
                projectInfo.AuthEndTime);

            if (updated)
            {
                _logger.LogInformation(
                    "DeviceStatusSignalRClient: Synced project license info from server. ProId={ProId}, ProName={ProName}",
                    license.ProjectId, projectInfo.ProName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "DeviceStatusSignalRClient: Failed to sync project license info from server.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();

        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
}
