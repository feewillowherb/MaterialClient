using System.Text.Json.Serialization;

namespace MaterialClient.Urban.Dtos;

public class XiaoshanUploadConfigDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("projectId")]
    public Guid ProjectId { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("remark")]
    public string? Remark { get; set; }

    [JsonPropertyName("modesJson")]
    public string ModesJson { get; set; } = "{}";

    [JsonPropertyName("settingsJson")]
    public string SettingsJson { get; set; } = "{}";
}

public class XiaoshanUploadConfigWriteDto
{
    [JsonPropertyName("projectId")]
    public Guid ProjectId { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("remark")]
    public string? Remark { get; set; }

    [JsonPropertyName("modesJson")]
    public string? ModesJson { get; set; }

    [JsonPropertyName("settingsJson")]
    public string? SettingsJson { get; set; }
}

/// <summary>
///     Result of a client save attempt (aligned vs draft retained).
/// </summary>
public record XiaoshanUploadConfigSaveResult(
    bool Success,
    bool IsAlignedWithServer,
    string? Message,
    XiaoshanUploadConfigDto? Config);
