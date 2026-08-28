using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Dtos.Urban;

public sealed record UrbanLprCaptureContext(
    string? PlateNumber,
    string? PlateColor,
    string? VehicleType,
    DateTime CapturedAt,
    PassageSource Source,
    UrbanInOutType InOutType,
    UrbanSiteType SiteType,
    int? LargeImageAttachmentId,
    int? SmallImageAttachmentId);
