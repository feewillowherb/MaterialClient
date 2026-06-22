using System.Text.Json.Serialization;

namespace MaterialClient.Common.Models;

/// <summary>
///     SignalR / pull payload when UrbanManagement Web approves a weighing record.
/// </summary>
public class WeighingRecordApprovedPushDto
{
    [JsonPropertyName("clientRecordId")]
    public long ClientRecordId { get; set; }

    [JsonPropertyName("plateNumber")]
    public string PlateNumber { get; set; } = string.Empty;

    /// <summary>Total weight in kilograms (kg), matching server storage.</summary>
    [JsonPropertyName("totalWeight")]
    public decimal TotalWeight { get; set; }

    [JsonPropertyName("serverApprovedAt")]
    public DateTime ServerApprovedAt { get; set; }

    [JsonPropertyName("editHistoryJson")]
    public string? EditHistoryJson { get; set; }
}
