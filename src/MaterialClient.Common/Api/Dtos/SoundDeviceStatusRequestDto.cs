using System.Text.Json.Serialization;

namespace MaterialClient.Common.Api.Dtos;

/// <summary>
///     Request body for ls21_mgrserver POST /api/devices/getDeviceBySN
/// </summary>
public record SoundDeviceStatusRequestDto
{
    /// <summary>
    ///     Request type, fixed value "req"
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "req";

    /// <summary>
    ///     Application identifier, fixed value "ls20"
    /// </summary>
    [JsonPropertyName("app")]
    public string App { get; init; } = "ls20";

    /// <summary>
    ///     Device serial number, format: "ls20://020021EA63AC"
    /// </summary>
    [JsonPropertyName("sn")]
    public string Sn { get; init; } = string.Empty;
}
