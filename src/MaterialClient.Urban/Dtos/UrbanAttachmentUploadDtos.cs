using System.Text.Json.Serialization;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Urban.Dtos;

/// <summary>
///     Attachment upload request (aligned with UrbanManagement UrbanAttachmentUploadInputDto).
/// </summary>
public class UrbanAttachmentUploadRequestDto
{
    [JsonPropertyName("buildLicenseNo")]
    public string BuildLicenseNo { get; set; } = string.Empty;

    [JsonPropertyName("attachType")]
    public AttachType AttachType { get; set; }

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

/// <summary>
///     Commit completed tus file ids to obtain AttachmentFile Guids.
/// </summary>
public class TusAttachmentCommitRequestDto
{
    [JsonPropertyName("fileIds")]
    public List<string> FileIds { get; set; } = [];
}
