using System.Text.Json.Serialization;

namespace MaterialClient.Common.Api.Dtos;

/// <summary>
///     Sound device play request
/// </summary>
public record SoundDevicePlayRequestDto
{
    /// <summary>
    ///     Request name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     Sound device serial number
    /// </summary>
    [JsonPropertyName("sn")]
    public string SerialNumber { get; init; } = string.Empty;

    /// <summary>
    ///     Request type
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    ///     Play parameters
    /// </summary>
    [JsonPropertyName("Params")]
    public SoundDevicePlayParamsDto Params { get; init; } = new();
}

/// <summary>
///     Sound device play parameters
/// </summary>
public record SoundDevicePlayParamsDto
{
    /// <summary>
    ///     User ID
    /// </summary>
    [JsonPropertyName("uid")]
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    ///     Volume (0-100)
    /// </summary>
    [JsonPropertyName("vol")]
    public int Volume { get; init; }

    /// <summary>
    ///     Audio URLs
    /// </summary>
    [JsonPropertyName("urls")]
    public SoundDevicePlayUrlDto[] Urls { get; init; } = [];

    /// <summary>
    ///     Priority level
    /// </summary>
    [JsonPropertyName("level")]
    public int Level { get; init; }

    /// <summary>
    ///     Task name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     Play count
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; init; }

    /// <summary>
    ///     Audio length
    /// </summary>
    [JsonPropertyName("length")]
    public int Length { get; init; }

    /// <summary>
    ///     Play type
    /// </summary>
    [JsonPropertyName("type")]
    public int Type { get; init; }

    /// <summary>
    ///     Task ID
    /// </summary>
    [JsonPropertyName("tid")]
    public string TaskId { get; init; } = string.Empty;
}

/// <summary>
///     Sound device play URL
/// </summary>
public record SoundDevicePlayUrlDto
{
    /// <summary>
    ///     URL name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     Whether to use UDP
    /// </summary>
    [JsonPropertyName("udp")]
    public bool Udp { get; init; }

    /// <summary>
    ///     Audio URI
    /// </summary>
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = string.Empty;
}

