namespace MaterialClient.UI;

/// <summary>
///     Controls which optional devices appear in the shared device status bar.
/// </summary>
public readonly record struct DeviceStatusBarOptions(
    bool DocumentCameraEnabled,
    bool PrinterEnabled,
    bool SoundDeviceEnabled)
{
    /// <summary>
    ///     Default for new deployments: only core weighing devices (scale, camera, LPR).
    /// </summary>
    public static DeviceStatusBarOptions CoreOnly => new(false, false, false);
}
