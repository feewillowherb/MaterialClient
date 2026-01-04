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

    /// <summary>
    ///     判断配置是否有效
    ///     当Enabled为true时，需要LocalIP、SoundIP和SoundSN都不为空
    /// </summary>
    /// <returns>如果配置有效返回true，否则返回false</returns>
    public bool IsValid()
    {
        if (!Enabled)
            return true; // 如果未启用，则认为配置有效（不需要验证）

        return !string.IsNullOrWhiteSpace(LocalIP) &&
               !string.IsNullOrWhiteSpace(SoundIP) &&
               !string.IsNullOrWhiteSpace(SoundSN);
    }
}

