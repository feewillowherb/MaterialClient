using System.Text.Json.Serialization;

namespace MaterialClient.Urban.Dtos;

/// <summary>
///     Attachment upload request (aligned with UrbanManagement UrbanAttachmentUploadInputDto).
/// </summary>
public class UrbanAttachmentUploadRequestDto
{
    [JsonPropertyName("buildLicenseNo")]
    public string BuildLicenseNo { get; set; } = string.Empty;

    [JsonPropertyName("attachType")]
    public string AttachType { get; set; } = string.Empty;

    [JsonPropertyName("images")]
    public string[] Images { get; set; } = [];
}

/// <summary>
///     Attachment upload response.
/// </summary>
public class UrbanAttachmentUploadResponseDto
{
    [JsonPropertyName("attachmentIds")]
    public List<Guid> AttachmentIds { get; set; } = [];
}
