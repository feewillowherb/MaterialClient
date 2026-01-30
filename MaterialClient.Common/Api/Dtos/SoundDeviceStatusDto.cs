using System.Text.Json.Serialization;

namespace MaterialClient.Common.Api.Dtos;

/// <summary>
///     Sound column device status response DTO
/// </summary>
public record SoundDeviceStatusDto
{
    /// <summary>
    ///     Device status code
    ///     0 - Offline
    ///     1 - Online
    ///     2 - In Task
    ///     3 - Power Off
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; init; }

    /// <summary>
    ///     Current task list
    /// </summary>
    [JsonPropertyName("tasks")]
    public IList<DeviceTaskInfo> Tasks { get; init; } = new List<DeviceTaskInfo>();
}

/// <summary>
///     Device task information
/// </summary>
public record DeviceTaskInfo
{
    // Task information structure (define based on actual API response)
    // Reserved field, may not be used in current version
}
