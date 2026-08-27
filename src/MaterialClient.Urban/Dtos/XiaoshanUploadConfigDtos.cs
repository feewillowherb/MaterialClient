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

    [JsonPropertyName("configVersion")]
    public long ConfigVersion { get; set; }
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

    [JsonPropertyName("expectedConfigVersion")]
    public long ExpectedConfigVersion { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("actor")]
    public string? Actor { get; set; }
}

public class XiaoshanUploadConfigWriteResultDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("isConflict")]
    public bool IsConflict { get; set; }

    [JsonPropertyName("config")]
    public XiaoshanUploadConfigDto? Config { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
///     Result of a client save attempt (aligned vs draft retained vs version conflict).
/// </summary>
public record XiaoshanUploadConfigSaveResult(
    bool Success,
    bool IsAlignedWithServer,
    bool IsConflict,
    string? Message,
    XiaoshanUploadConfigDto? Config);
