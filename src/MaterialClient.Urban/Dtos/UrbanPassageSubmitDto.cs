using System.Text.Json.Serialization;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;

namespace MaterialClient.Urban.Dtos;

/// <summary>
///     Passage ingest payload aligned with UrbanManagement UrbanPassageReceiveFields.
/// </summary>
public sealed record UrbanPassageSubmitDto
{
    [JsonPropertyName("clientRecordId")]
    public Guid ClientRecordId { get; init; }

    [JsonPropertyName("plateNumber")]
    public string? PlateNumber { get; init; }

    [JsonPropertyName("plateColor")]
    public string? PlateColor { get; init; }

    [JsonPropertyName("vehicleType")]
    public string? VehicleType { get; init; }

    [JsonPropertyName("capturedAt")]
    public DateTime CapturedAt { get; init; }

    [JsonPropertyName("urbanInOutType")]
    public UrbanInOutType UrbanInOutType { get; init; }

    [JsonPropertyName("urbanSiteType")]
    public UrbanSiteType UrbanSiteType { get; init; }

    [JsonPropertyName("buildLicenseNo")]
    public string? BuildLicenseNo { get; init; }

    [JsonPropertyName("proId")]
    public Guid ProId { get; init; }

    [JsonPropertyName("proName")]
    public string? ProName { get; init; }

    [JsonPropertyName("submitMachineCode")]
    public string? SubmitMachineCode { get; init; }

    [JsonPropertyName("attachmentIds")]
    public List<Guid>? AttachmentIds { get; init; }

    public static UrbanPassageSubmitDto FromPassage(
        UrbanPassageRecord record,
        LicenseInfo? license,
        string submitMachineCode,
        IReadOnlyList<Guid> attachmentIds)
    {
        return new UrbanPassageSubmitDto
        {
            ClientRecordId = record.Id,
            PlateNumber = record.PlateNumber,
            PlateColor = record.PlateColor,
            VehicleType = record.VehicleType,
            CapturedAt = record.CapturedAt,
            UrbanInOutType = record.UrbanInOutType,
            UrbanSiteType = record.UrbanSiteType,
            BuildLicenseNo = license?.AccessCode,
            ProId = license?.ProjectId ?? Guid.Empty,
            ProName = license?.ProName,
            SubmitMachineCode = submitMachineCode,
            AttachmentIds = attachmentIds.Count > 0 ? attachmentIds.ToList() : null
        };
    }
}

public sealed record UrbanPassageReceiveResult
{
    [JsonPropertyName("recordId")]
    public Guid RecordId { get; init; }
}
