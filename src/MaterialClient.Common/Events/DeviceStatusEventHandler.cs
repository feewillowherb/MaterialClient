using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Authentication;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Events;

/// <summary>
///     Handles DeviceStatusChangedEventData events and forwards them
///     to the server via SignalR.
///     Reads ProId and ProName from LicenseInfo to fill into the device status message.
///     Implements client-side throttling: max 1 message per device type per second.
/// </summary>
[AutoConstructor]
public partial class DeviceStatusEventHandler :
    ILocalEventHandler<DeviceStatusChangedEventData>,
    ITransientDependency
{
    private readonly ILogger<DeviceStatusEventHandler> _logger;
    private readonly IDeviceStatusSignalRClient _signalRClient;
    private readonly ILicenseService _licenseService;

    /// <summary>
    ///     Tracks last send time per device type for throttling.
    /// </summary>
    private static readonly Dictionary<string, DateTime> _lastSendTimes = new();

    /// <summary>
    ///     Tracks latest status per device type for status merging.
    /// </summary>
    private static readonly Dictionary<string, DeviceStatusChangedEventData> _latestStatus = new();

    /// <summary>
    ///     Lock for thread-safe throttle tracking.
    /// </summary>
    private static readonly object _throttleLock = new();

    /// <summary>
    ///     Minimum interval between sends for the same device type.
    /// </summary>
    private static readonly TimeSpan MinSendInterval = TimeSpan.FromSeconds(1);

    [UnitOfWork]
    public async Task HandleEventAsync(DeviceStatusChangedEventData eventData)
    {
        _logger.LogDebug(
            "DeviceStatusEventHandler: Received device status change. DeviceType={DeviceType}, Status={Status}",
            eventData.DeviceType, eventData.Status);

        // Check throttling - max 1 message per device type per second
        if (ShouldThrottle(eventData.DeviceType, eventData, out var latestStatus))
        {
            _logger.LogDebug(
                "DeviceStatusEventHandler: Throttled. Merging status for DeviceType={DeviceType}. Latest Status={Status}",
                eventData.DeviceType, latestStatus.Status);

            // Use the latest status (merge)
            eventData = latestStatus;
        }

        // Read ProId and ProName from LicenseInfo
        string proId = string.Empty;
        string proName = string.Empty;
        try
        {
            var licenseInfo = await _licenseService.GetCurrentLicenseAsync();
            if (licenseInfo != null)
            {
                proId = licenseInfo.ProjectId.ToString();
                proName = licenseInfo.ProName ?? string.Empty;
            }
            else
            {
                _logger.LogDebug("DeviceStatusEventHandler: LicenseInfo not available, ProId/ProName will be empty");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DeviceStatusEventHandler: Failed to read LicenseInfo, ProId/ProName will be empty");
        }

        // Build device status message
        var message = new DeviceStatusMessage
        {
            ClientId = Environment.MachineName,
            ProId = proId,
            ProName = proName,
            DeviceType = eventData.DeviceType,
            Status = eventData.Status,
            Timestamp = DateTime.UtcNow,
            AdditionalData = eventData.AdditionalData
        };

        // Send via SignalR client
        try
        {
            await _signalRClient.UploadStatusAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "DeviceStatusEventHandler: Failed to upload device status for DeviceType={DeviceType}",
                eventData.DeviceType);
        }
    }

    /// <summary>
    ///     Checks if a message should be throttled for the given device type.
    ///     Returns true if throttled, false if should send.
    ///     When throttled, updates the latest status for merging.
    /// </summary>
    private bool ShouldThrottle(
        string deviceType,
        DeviceStatusChangedEventData current,
        out DeviceStatusChangedEventData latestStatus)
    {
        lock (_throttleLock)
        {
            // Always track the latest status for merging
            _latestStatus[deviceType] = current;
            latestStatus = current;

            if (_lastSendTimes.TryGetValue(deviceType, out var lastSend))
            {
                var elapsed = DateTime.UtcNow - lastSend;
                if (elapsed < MinSendInterval)
                {
                    return true; // Throttle
                }
            }

            // Update last send time
            _lastSendTimes[deviceType] = DateTime.UtcNow;
            return false;
        }
    }
}
