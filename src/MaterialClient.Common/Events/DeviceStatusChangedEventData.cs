namespace MaterialClient.Common.Events;

/// <summary>
///     Event data for device status changes, published via ILocalEventBus.
///     Used to notify the SignalR client about device online/offline transitions.
/// </summary>
public class DeviceStatusChangedEventData
{
    public DeviceStatusChangedEventData(string deviceType, string status, string? additionalData = null)
    {
        DeviceType = deviceType;
        Status = status;
        AdditionalData = additionalData;
    }

    /// <summary>
    ///     Device type (e.g., "Scale", "Camera", "LPR", "Sound").
    /// </summary>
    public string DeviceType { get; }

    /// <summary>
    ///     Status value (e.g., "Online", "Offline").
    /// </summary>
    public string Status { get; }

    /// <summary>
    ///     Optional additional data (e.g., error details).
    /// </summary>
    public string? AdditionalData { get; }
}
