using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Services;

/// <summary>
///     Unified LPR device interface providing active capture capability.
/// </summary>
/// <remarks>
///     Defines the active capture standard for all LPR device types.
///     Support varies by vendor: Hikvision (supported), LprAllInOne (supported), Huaxiazhixin (not supported).
///     Recognition results are delivered only via MessageBus <see cref="Events.LicensePlateRecognizedMessage"/>.
/// </remarks>
public interface ILprDevice
{
    /// <summary>
    ///     Triggers a single license plate recognition capture for the given device.
    /// </summary>
    /// <param name="config">Device configuration.</param>
    /// <returns>A task that completes when the trigger has been sent; recognition result is delivered via MessageBus.</returns>
    /// <remarks>
    ///     Implementations must: login/auth, send trigger command, then return.
    ///     Throw <see cref="NotSupportedException"/> when the device does not support active capture.
    /// </remarks>
    Task TriggerCaptureAsync(LicensePlateRecognitionConfig config);

    /// <summary>
    ///     设备是否支持主动抓拍
    /// </summary>
    /// <value>
    ///     如果设备支持通过应用触发抓拍,则为 true;
    ///     如果设备仅支持被动捕获(设备推送)或厂商限制,则为 false。
    /// </value>
    /// <remarks>
    ///     在调用 <see cref="TriggerCaptureAsync"/> 之前,应先检查此属性。
    ///     如果为 false,调用 TriggerCaptureAsync 将会抛出 NotSupportedException。
    /// </remarks>
    bool SupportsActiveCapture { get; }
}
