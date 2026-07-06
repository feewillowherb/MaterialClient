using System.Text.Json.Serialization;

namespace MaterialClient.Urban.Dtos;

public class AckApprovalSyncDto
{
    [JsonPropertyName("clientRecordId")]
    public Guid ClientRecordId { get; set; }
}

public class PendingServerApprovalSyncQueryDto
{
    [JsonPropertyName("proId")]
    public Guid ProId { get; set; }
}
