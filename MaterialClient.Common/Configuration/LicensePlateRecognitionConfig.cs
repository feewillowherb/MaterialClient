using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Configuration;

/// <summary>
///     License plate recognition configuration
/// </summary>
public class LicensePlateRecognitionConfig
{
    /// <summary>
    ///     Recognition device name (e.g., camera_1)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Device IP address (e.g., 192.168.3.245)
    /// </summary>
    public string Ip { get; set; } = string.Empty;

    /// <summary>
    ///     Recognition direction (In or Out)
    /// </summary>
    public LicensePlateDirection Direction { get; set; } = LicensePlateDirection.In;
}