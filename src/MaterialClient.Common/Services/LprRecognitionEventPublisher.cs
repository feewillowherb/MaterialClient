using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services.Vzvision;
using Microsoft.Extensions.Logging;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Common.Services;

/// <summary>
///     Payload for one physical LPR recognition before DeviceName fan-out.
/// </summary>
public sealed record LprRecognitionPayload(
    string PlateNumber,
    VzvisionColorType? ColorType,
    string? VehicleColor,
    string? VehicleType,
    string? PlateColor,
    LprDeviceType DeviceType,
    DateTime Timestamp,
    string? LprImagePath);

/// <summary>
///     Publishes <see cref="LicensePlateRecognizedEventData"/> with shared-IP fan-out (static; no DI).
/// </summary>
public static class LprRecognitionEventPublisher
{
    public static async Task PublishFanOutAsync(
        ILocalEventBus localEventBus,
        IReadOnlyList<LicensePlateRecognitionConfig>? settingsConfigs,
        string deviceIp,
        LicensePlateRecognitionConfig? sdkFallbackConfig,
        LprRecognitionPayload payload,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(localEventBus);
        ArgumentNullException.ThrowIfNull(payload);

        var rows = LicensePlateRecognitionConfig.FindAllByIp(settingsConfigs, deviceIp);
        if (rows.Count == 0)
        {
            var fallbackName = sdkFallbackConfig?.Name;
            if (string.IsNullOrWhiteSpace(fallbackName))
                fallbackName = string.IsNullOrWhiteSpace(deviceIp) ? "Unknown" : $"Unknown ({deviceIp})";

            await localEventBus.PublishAsync(CreateEvent(payload, fallbackName, deviceIp));
            logger?.LogDebug(
                "LPR fan-out fallback single event: Ip={Ip}, Device={Device}, Plate={Plate}",
                deviceIp, fallbackName, payload.PlateNumber);
            return;
        }

        foreach (var row in rows)
        {
            await localEventBus.PublishAsync(CreateEvent(payload, row.Name, deviceIp));
        }

        if (rows.Count > 1)
        {
            logger?.LogInformation(
                "LPR fan-out published {Count} events for Ip={Ip}, Plate={Plate}, Devices={Devices}",
                rows.Count,
                deviceIp,
                payload.PlateNumber,
                string.Join(", ", rows.Select(r => r.Name)));
        }
    }

    private static LicensePlateRecognizedEventData CreateEvent(
        LprRecognitionPayload payload,
        string deviceName,
        string? deviceIp) =>
        new()
        {
            PlateNumber = payload.PlateNumber,
            ColorType = payload.ColorType,
            VehicleColor = payload.VehicleColor,
            VehicleType = payload.VehicleType,
            PlateColor = payload.PlateColor,
            DeviceType = payload.DeviceType,
            DeviceName = deviceName,
            DeviceIp = string.IsNullOrWhiteSpace(deviceIp) ? null : deviceIp.Trim(),
            Timestamp = payload.Timestamp,
            LprImagePath = payload.LprImagePath
        };
}
