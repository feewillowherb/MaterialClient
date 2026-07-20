using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Events;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services.Authentication;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

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

    /// <summary>
    ///     Whether the connection is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    ///     Register a callback for receiving log list requests from the server.
    /// </summary>
    void OnReceiveLogListRequest(Func<string, string, string, Task> callback);

    /// <summary>
    ///     Register the client's log pull capability with the server.
    /// </summary>
    /// <param name="clientId">客户端唯一标识符</param>
    /// <param name="capabilityInfo">日志能力信息</param>
    /// <param name="proName">客户端所属项目名称，用于服务端展示「项目名称-客户端名称」</param>
    Task RegisterLogCapability(string clientId, object capabilityInfo, string proName);

    /// <summary>
    ///     Return the log list result to the server.
    /// </summary>
    Task ReturnLogList(object result);

    /// <summary>
    ///     Register a callback for receiving file content requests from the server.
    /// </summary>
    void OnReceiveFileContentRequest(Func<string, string, string, string, Task> callback);

    /// <summary>
    ///     Return a file chunk to the server.
    /// </summary>
    Task ReturnFileChunkAsync(string requestId, int chunkIndex, int totalChunks, byte[] data, long totalFileSize);

    /// <summary>
    ///     Return a file error to the server.
    /// </summary>
    Task ReturnFileErrorAsync(string requestId, string errorMessage);

    /// <summary>
    ///     Register a callback for server Web approval sync push messages.
    /// </summary>
    void OnWeighingRecordApproved(Func<Models.WeighingRecordApprovedPushDto, Task> callback);
}

/// <inheritdoc />
public class DeviceStatusSignalRClient : IDeviceStatusSignalRClient, IAsyncDisposable
{
    private readonly ILogger<DeviceStatusSignalRClient> _logger;
    private readonly ILicenseService _licenseService;
    private readonly IStaticLicenseChecker _staticLicenseChecker;
    private readonly ILocalEventBus _localEventBus;
    private readonly IDeviceStatusRepublishTrigger? _republishTrigger;
    private readonly SignalRClientOptions _options;

    private HubConnection? _connection;
    private readonly ConcurrentQueue<DeviceStatusMessage> _messageQueue = new();
    private CancellationTokenSource? _reconnectCts;
    private int _reconnectAttempts;
    private bool _isStarted;
    private bool _isStopped;
    private readonly object _lock = new();

    private Func<string, string, string, Task>? _logListRequestHandler;
    private Func<string, string, string, string, Task>? _fileContentRequestHandler;
    private Func<Models.WeighingRecordApprovedPushDto, Task>? _weighingRecordApprovedHandler;
    private bool _logListHandlerRegistered;
    private bool _fileContentHandlerRegistered;
    private bool _weighingRecordApprovedHandlerRegistered;

    public DeviceStatusSignalRClient(
        ILogger<DeviceStatusSignalRClient> logger,
        ILicenseService licenseService,
        IStaticLicenseChecker staticLicenseChecker,
        ILocalEventBus localEventBus,
        IOptions<SignalRClientOptions> options,
        IDeviceStatusRepublishTrigger? republishTrigger = null)
    {
        _logger = logger;
        _licenseService = licenseService;
        _staticLicenseChecker = staticLicenseChecker;
        _localEventBus = localEventBus;
        _republishTrigger = republishTrigger;
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
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

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

        TryRegisterLogPullHandlers();
        RegisterUpdateClientLicenseHandler();

        try
        {
            await _connection.StartAsync(cancellationToken);
            _isStarted = true;
            _logger.LogInformation(
                "DeviceStatusSignalRClient: Connected successfully. ConnectionId={ConnectionId}",
                _connection.ConnectionId);

            await OnConnectionRestoredAsync();
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
        if (delays.Length == 0)
        {
            delays = [0, 2, 10, 30];
        }

        var maxAttempts = _options.MaxReconnectAttempts;
        var persistentReconnect = _options.PersistentReconnect;

        while (!cancellationToken.IsCancellationRequested &&
               (persistentReconnect || _reconnectAttempts < maxAttempts))
        {
            _reconnectAttempts++;

            var delaySeconds = persistentReconnect
                ? delays[(_reconnectAttempts - 1) % delays.Length]
                : _reconnectAttempts <= delays.Length
                    ? delays[_reconnectAttempts - 1]
                    : Math.Min((int)Math.Pow(2, _reconnectAttempts), 60);

            if (persistentReconnect)
            {
                _logger.LogDebug(
                    "DeviceStatusSignalRClient: Reconnect attempt {Attempt} in {Delay}s (persistent)",
                    _reconnectAttempts, delaySeconds);
            }
            else
            {
                _logger.LogDebug(
                    "DeviceStatusSignalRClient: Reconnect attempt {Attempt}/{Max} in {Delay}s",
                    _reconnectAttempts, maxAttempts, delaySeconds);
            }

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

                    await OnConnectionRestoredAsync();
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

        if (!persistentReconnect && _reconnectAttempts >= maxAttempts)
        {
            _logger.LogError(
                "DeviceStatusSignalRClient: Max reconnect attempts ({Max}) reached. Giving up. " +
                "Set SignalR:PersistentReconnect to true to keep retrying.",
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

        await OnConnectionRestoredAsync();
    }

    private async Task OnConnectionRestoredAsync()
    {
        await FlushMessageQueueAsync();
        await SyncProjectLicenseFromServerAsync();
        await _localEventBus.PublishAsync(new SignalRConnectionRestoredEventData());
        _republishTrigger?.RepublishActiveStatuses();
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

            // Step 1: Read local JWT (priority: LatestJwtToken > .urban file) for anti-tamper check
            var licenseFilePath = "license.urban";
            var localJwt = await _licenseService.GetLocalJwtTokenAsync(licenseFilePath);

            bool antiTamperPassed = false;

            if (!string.IsNullOrWhiteSpace(localJwt))
            {
                // Step 2: Submit JWT to server for anti-tamper verification
                try
                {
                    var antiTamperResult = await _connection.InvokeAsync<JwtAntiTamperResult>(
                        "VerifyJwtAsync",
                        localJwt,
                        license.ProjectId.ToString());

                    if (antiTamperResult.Passed && !string.IsNullOrEmpty(antiTamperResult.ServerJwt))
                    {
                        // Step 3: Store server JWT and derive LicenseInfo from server JWT claims
                        await _licenseService.StoreServerJwtAsync(
                            antiTamperResult.ServerJwt,
                            antiTamperResult.ProName ?? license.ProName ?? string.Empty,
                            antiTamperResult.BuildLicenseNo,
                            antiTamperResult.AuthEndTime ?? license.AuthEndTime);

                        antiTamperPassed = true;

                        _logger.LogInformation(
                            "DeviceStatusSignalRClient: Anti-tamper check passed. Server JWT stored. ProId={ProId}",
                            license.ProjectId);
                    }
                    else
                    {
                        // F4: A device change is an irreversible business fact — the project was
                        // re-activated on another device, so this (old-device) token is revoked.
                        // Clear the local JWT and hand off to the UI layer to force re-activation
                        // (terminating if the user cancels). Other failure types keep the
                        // availability-first "log + skip" behaviour below.
                        if (antiTamperResult.RevocationReason == RevocationReason.DeviceChanged)
                        {
                            _logger.LogWarning(
                                "DeviceStatusSignalRClient: Authorization device changed. ProId={ProId}, Reason={Reason}. " +
                                "Clearing local JWT and requesting re-activation.",
                                license.ProjectId, antiTamperResult.Reason ?? "Unknown");

                            await _licenseService.ClearLatestJwtTokenAsync();
                            _ = _localEventBus.PublishAsync(new LicenseDeviceRevokedEto(
                                license.ProjectId,
                                antiTamperResult.Reason ?? "授权设备已变更，请在当前设备重新激活"));
                            return; // Do NOT proceed with field sync; app re-activates or exits.
                        }

                        if (antiTamperResult.RevocationReason == RevocationReason.Expired)
                        {
                            var handled = await TryHandleExpiredAuthorizationAsync(
                                license,
                                antiTamperResult);
                            if (handled)
                            {
                                return;
                            }
                        }

                        // Non-device-change failure (NOT_FOUND/INVALID_SIGNATURE/UNREACHABLE):
                        // do NOT modify LicenseInfo, keep availability-first skip.
                        _logger.LogWarning(
                            "DeviceStatusSignalRClient: Anti-tamper check FAILED. ProId={ProId}, Reason={Reason}, RevocationReason={RevocationReason}. Skipping LicenseInfo update.",
                            license.ProjectId, antiTamperResult.Reason ?? "Unknown", antiTamperResult.RevocationReason);
                        return; // Do NOT proceed with field sync
                    }
                }
                catch (TimeoutException ex)
                {
                    // SignalR timeout: fall back to field sync only (availability over strict verification)
                    _logger.LogWarning(ex,
                        "DeviceStatusSignalRClient: VerifyJwtAsync call failed (timeout). ProId={ProId}. Falling back to field sync only.",
                        license.ProjectId);
                }
                catch (Exception ex)
                {
                    // Network exception: fall back to field sync only
                    _logger.LogWarning(ex,
                        "DeviceStatusSignalRClient: VerifyJwtAsync call failed. ProId={ProId}. Falling back to field sync only.",
                        license.ProjectId);
                }
            }
            else
            {
                // No local JWT available: skip anti-tamper check, proceed with field sync
                _logger.LogDebug(
                    "DeviceStatusSignalRClient: No local JWT available for anti-tamper check. ProId={ProId}. Proceeding with field sync only.",
                    license.ProjectId);
            }

            // If anti-tamper passed, server JWT is already stored — skip field sync
            if (antiTamperPassed)
            {
                return;
            }

            // Step 4: Existing field sync (GetClientProjectLicenseInfo) for cases without JWT
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

    /// <summary>
    ///     Clears local JWT and persists server-authoritative expiry fields, then notifies Urban to terminate.
    /// </summary>
    /// <returns><c>true</c> when the expired path was handled (caller should stop sync).</returns>
    private async Task<bool> TryHandleExpiredAuthorizationAsync(
        LicenseInfo license,
        JwtAntiTamperResult antiTamperResult)
    {
        var authEndTime = antiTamperResult.AuthEndTime ?? license.AuthEndTime;
        var message = string.IsNullOrWhiteSpace(antiTamperResult.Reason)
            ? "授权已过期"
            : antiTamperResult.Reason;

        await _licenseService.ApplyServerExpirationAsync(
            authEndTime,
            antiTamperResult.ProName ?? license.ProName,
            antiTamperResult.BuildLicenseNo ?? license.AccessCode);

        _logger.LogInformation(
            "DeviceStatusSignalRClient: Cleared JWT after server expiry. ProId={ProId}, AuthEndTime={AuthEndTime}",
            license.ProjectId,
            authEndTime);

        _ = _localEventBus.PublishAsync(new LicenseExpiredEto(license.ProjectId, message));
        return true;
    }

    private void RegisterUpdateClientLicenseHandler()
    {
        if (_connection == null)
        {
            return;
        }

        _connection.On<ClientLicenseUpdateDto>("UpdateClientLicense", async dto =>
        {
            if (string.IsNullOrWhiteSpace(dto.JwtToken))
            {
                _logger.LogWarning("DeviceStatusSignalRClient: UpdateClientLicense received empty JWT");
                return;
            }

            var checkResult = await _staticLicenseChecker.CheckLicenseFromTokenAsync(dto.JwtToken);
            if (!checkResult.IsSuccess)
            {
                _logger.LogWarning(
                    "DeviceStatusSignalRClient: UpdateClientLicense JWT validation failed: {Reason}",
                    checkResult.Message);
                return;
            }

            await _licenseService.StoreServerJwtAsync(
                dto.JwtToken.Trim(),
                checkResult.ProName ?? string.Empty,
                checkResult.AccessCode,
                checkResult.AuthEndTime);

            _logger.LogInformation(
                "DeviceStatusSignalRClient: License updated via UpdateClientLicense push. ProId={ProId}",
                checkResult.ProId);
        });
    }

    /// <inheritdoc />
    public void OnReceiveLogListRequest(Func<string, string, string, Task> callback)
    {
        _logListRequestHandler = callback;
        TryRegisterLogListHandler();
    }

    private void TryRegisterLogListHandler()
    {
        if (_connection == null || _logListRequestHandler == null || _logListHandlerRegistered)
        {
            return;
        }

        _connection.On<string, string, string>("ReceiveLogListRequest",
            (requestId, targetClientId, dateFolder) =>
                _logListRequestHandler(requestId, targetClientId, dateFolder));
        _logListHandlerRegistered = true;
        _logger.LogDebug("DeviceStatusSignalRClient: Registered ReceiveLogListRequest callback");
    }

    /// <inheritdoc />
    public void OnReceiveFileContentRequest(Func<string, string, string, string, Task> callback)
    {
        _fileContentRequestHandler = callback;
        TryRegisterFileContentHandler();
    }

    private void TryRegisterFileContentHandler()
    {
        if (_connection == null || _fileContentRequestHandler == null || _fileContentHandlerRegistered)
        {
            return;
        }

        _connection.On<string, string, string, string>("ReceiveFileContentRequest",
            (requestId, targetClientId, filePath, fileName) =>
                _fileContentRequestHandler(requestId, targetClientId, filePath, fileName));
        _fileContentHandlerRegistered = true;
        _logger.LogDebug("DeviceStatusSignalRClient: Registered ReceiveFileContentRequest callback");
    }

    /// <inheritdoc />
    public void OnWeighingRecordApproved(Func<Models.WeighingRecordApprovedPushDto, Task> callback)
    {
        _weighingRecordApprovedHandler = callback;
        TryRegisterWeighingRecordApprovedHandler();
    }

    private void TryRegisterWeighingRecordApprovedHandler()
    {
        if (_connection == null || _weighingRecordApprovedHandler == null || _weighingRecordApprovedHandlerRegistered)
        {
            return;
        }

        _connection.On<Models.WeighingRecordApprovedPushDto>(
            "WeighingRecordApproved",
            push => _weighingRecordApprovedHandler(push));
        _weighingRecordApprovedHandlerRegistered = true;
        _logger.LogDebug("DeviceStatusSignalRClient: Registered WeighingRecordApproved callback");
    }

    private void TryRegisterLogPullHandlers()
    {
        TryRegisterLogListHandler();
        TryRegisterFileContentHandler();
        TryRegisterWeighingRecordApprovedHandler();
    }

    /// <inheritdoc />
    public async Task RegisterLogCapability(string clientId, object capabilityInfo, string proName)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _connection.InvokeAsync("RegisterLogCapability", clientId, capabilityInfo, proName);
                _logger.LogDebug("DeviceStatusSignalRClient: Registered log capability for ClientId={ClientId}, ProName={ProName}", clientId, proName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DeviceStatusSignalRClient: Failed to register log capability");
                throw;
            }
        }
        else
        {
            throw new InvalidOperationException("Cannot register log capability: connection is not established");
        }
    }

    /// <inheritdoc />
    public async Task ReturnLogList(object result)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _connection.InvokeAsync("ReturnLogList", result);
                _logger.LogDebug("DeviceStatusSignalRClient: Returned log list for RequestId={RequestId}",
                    result.GetType().GetProperty("RequestId")?.GetValue(result));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DeviceStatusSignalRClient: Failed to return log list");
                throw;
            }
        }
        else
        {
            throw new InvalidOperationException("Cannot return log list: connection is not established");
        }
    }

    /// <inheritdoc />
    public async Task ReturnFileChunkAsync(string requestId, int chunkIndex, int totalChunks, byte[] data, long totalFileSize)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _connection.InvokeAsync("ReceiveFileChunk", requestId, chunkIndex, totalChunks, data, totalFileSize);
                _logger.LogDebug("DeviceStatusSignalRClient: Sent file chunk {ChunkIndex}/{TotalChunks} for RequestId={RequestId}",
                    chunkIndex, totalChunks, requestId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DeviceStatusSignalRClient: Failed to send file chunk");
                throw;
            }
        }
        else
        {
            throw new InvalidOperationException("Cannot send file chunk: connection is not established");
        }
    }

    /// <inheritdoc />
    public async Task ReturnFileErrorAsync(string requestId, string errorMessage)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _connection.InvokeAsync("ReceiveFileError", requestId, errorMessage);
                _logger.LogWarning("DeviceStatusSignalRClient: Sent file error for RequestId={RequestId}: {ErrorMessage}",
                    requestId, errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DeviceStatusSignalRClient: Failed to send file error");
                throw;
            }
        }
        else
        {
            throw new InvalidOperationException("Cannot send file error: connection is not established");
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
