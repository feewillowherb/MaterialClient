using System.Text.Json.Serialization;

namespace MaterialClient.Common.Api.Dtos;

/// <summary>
///     Sound device settings configuration
/// </summary>
public record SoundDeviceSettingsDto
{
    /// <summary>
    ///     Local IP address for TTS service
    /// </summary>
    [JsonPropertyName("localIP")]
    public string LocalIP { get; init; } = string.Empty;

    /// <summary>
    ///     Sound device IP address
    /// </summary>
    [JsonPropertyName("soundIP")]
    public string SoundIP { get; init; } = string.Empty;

    /// <summary>
    ///     Sound device serial number
    /// </summary>
    [JsonPropertyName("soundSN")]
    public string SoundSN { get; init; } = string.Empty;

    /// <summary>
    ///     Sound volume (0-100, "0" means 100)
    /// </summary>
    [JsonPropertyName("soundVolume")]
    public string SoundVolume { get; init; } = "0";
}

