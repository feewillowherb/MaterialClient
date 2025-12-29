namespace MaterialClient.Common.Configuration;

/// <summary>
///     Sound device settings configuration
/// </summary>
public class SoundDeviceSettings
{
    /// <summary>
    ///     Local IP address for TTS service
    /// </summary>
    public string LocalIP { get; set; } = string.Empty;

    /// <summary>
    ///     Sound device IP address
    /// </summary>
    public string SoundIP { get; set; } = string.Empty;

    /// <summary>
    ///     Sound device serial number
    /// </summary>
    public string SoundSN { get; set; } = string.Empty;

    /// <summary>
    ///     Sound volume (0-100, "0" means 100)
    /// </summary>
    public string SoundVolume { get; set; } = "0";

    /// <summary>
    ///     Enable sound device functionality
    /// </summary>
    public bool Enabled { get; set; } = false;
}

