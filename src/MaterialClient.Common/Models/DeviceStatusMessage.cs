using System.Text.Json.Serialization;

namespace MaterialClient.Common.Models;

/// <summary>
///     Device status message DTO for SignalR communication.
///     Must match the server-side DeviceStatusMessage structure.
/// </summary>
public record DeviceStatusMessage
{
    /// <summary>
    ///     Client unique identifier (machine code or configured value).
    /// </summary>
    [JsonPropertyName("clientId")]
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    ///     项目主键（用于聚合、缓存、筛选，从 LicenseInfo.ProjectId 读取）
    /// </summary>
    [JsonPropertyName("proId")]
    public string ProId { get; init; } = string.Empty;

    /// <summary>
    ///     项目展示名称（从 LicenseInfo.ProName 读取）
    /// </summary>
    [JsonPropertyName("proName")]
    public string ProName { get; init; } = string.Empty;

    /// <summary>
    ///     Device type (e.g., "Scale", "Camera", "LPR", "Sound").
    /// </summary>
    [JsonPropertyName("deviceType")]
    public string DeviceType { get; init; } = string.Empty;

    /// <summary>
    ///     Status value (e.g., "Online", "Offline").
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>
    ///     Timestamp of the status change.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    /// <summary>
    ///     Optional additional data.
    /// </summary>
    [JsonPropertyName("additionalData")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AdditionalData { get; init; }
}
