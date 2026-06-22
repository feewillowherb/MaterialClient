using System.Text.Json.Serialization;

namespace MaterialClient.Urban.Dtos;

public class AckApprovalSyncDto
{
    [JsonPropertyName("clientRecordId")]
    public long ClientRecordId { get; set; }
}

public class PendingServerApprovalSyncQueryDto
{
    [JsonPropertyName("proId")]
    public Guid ProId { get; set; }
}
