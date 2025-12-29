using System.Text.Json.Serialization;

namespace MaterialClient.Common.Api.Dtos;

/// <summary>
///     Sound device play success response
/// </summary>
public record SoundDevicePlaySuccessResponseDto
{
    /// <summary>
    ///     Response code (0 indicates success)
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; init; }

    /// <summary>
    ///     Response type
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    ///     Response message
    /// </summary>
    [JsonPropertyName("msg")]
    public string Msg { get; init; } = string.Empty;

    /// <summary>
    ///     Request name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

/// <summary>
///     Sound device play error response
/// </summary>
public record SoundDevicePlayErrorResponseDto
{
    /// <summary>
    ///     Error result code (-1 indicates failure)
    /// </summary>
    [JsonPropertyName("result")]
    public int Result { get; init; }

    /// <summary>
    ///     Error message
    /// </summary>
    [JsonPropertyName("msg")]
    public string Msg { get; init; } = string.Empty;
}

