using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Dtos.Urban;

public class UrbanAttendedListRow
{
    public const string UnrecognizedPlateDisplay = "未识别";
    public const string EmDash = "—";

    public UrbanAttendedListKind Kind { get; init; }

    public long? WeighingRecordId { get; init; }

    public Guid? PassageRecordId { get; init; }

    public string DisplayPlate { get; init; } = string.Empty;

    public string? PlateColor { get; init; }

    public string? VehicleType { get; init; }

    public string KindLabel { get; init; } = string.Empty;

    public string WeightText { get; init; } = EmDash;

    public string InOutText { get; init; } = EmDash;

    public string SiteTypeText { get; init; } = EmDash;

    public DateTime SortTime { get; init; }

    public string StatusText { get; init; } = EmDash;

    public bool IsAnomaly { get; init; }

    public SyncStatus? SyncStatus { get; init; }

    public AnomalyReason? AnomalyReason { get; init; }

    public DateTime? UploadTime { get; init; }

    public decimal TotalWeight { get; init; }

    public DateTime AddDate { get; init; }

    public bool ShowApprove { get; init; }

    public int? LargeImageAttachmentId { get; init; }

    public string? LargePhotoPath { get; init; }

    public bool IsWeighingKind => Kind == UrbanAttendedListKind.Weighing;

    public bool IsPassageKind => !IsWeighingKind;

    public static UrbanAttendedListRow FromWeighing(UrbanWeighingListItemDto dto)
    {
        return new UrbanAttendedListRow
        {
            Kind = UrbanAttendedListKind.Weighing,
            WeighingRecordId = dto.WeighingRecordId,
            DisplayPlate = dto.PlateNumber ?? string.Empty,
            KindLabel = "地磅",
            WeightText = $"{dto.TotalWeight:F2} 吨",
            InOutText = EmDash,
            SortTime = dto.AddDate,
            StatusText = dto.IsAnomaly ? "异常" : "正常",
            IsAnomaly = dto.IsAnomaly,
            SyncStatus = dto.SyncStatus,
            AnomalyReason = dto.AnomalyReason,
            UploadTime = dto.UploadTime,
            ShowApprove = dto.IsAnomaly,
            TotalWeight = dto.TotalWeight,
            AddDate = dto.AddDate
        };
    }

    public static UrbanAttendedListRow FromPassage(
        Guid id,
        PassageSource source,
        string plateNumber,
        string plateColor,
        string vehicleType,
        UrbanInOutType inOutType,
        UrbanSiteType siteType,
        DateTime capturedAt,
        int? largeImageAttachmentId,
        string? largePhotoPath)
    {
        var kind = source == PassageSource.FinishedProduct
            ? UrbanAttendedListKind.FinishedProduct
            : UrbanAttendedListKind.Checkpoint;

        return new UrbanAttendedListRow
        {
            Kind = kind,
            PassageRecordId = id,
            DisplayPlate = FormatStoredPlate(plateNumber),
            PlateColor = plateColor,
            VehicleType = vehicleType,
            KindLabel = kind == UrbanAttendedListKind.FinishedProduct ? "成品" : "卡口",
            WeightText = EmDash,
            InOutText = inOutType == UrbanInOutType.Exit ? "出" : "进",
            SiteTypeText = siteType == UrbanSiteType.Disposal ? "消纳" : "工地",
            SortTime = capturedAt,
            StatusText = EmDash,
            ShowApprove = false,
            LargeImageAttachmentId = largeImageAttachmentId,
            LargePhotoPath = largePhotoPath
        };
    }

    public static string FormatStoredPlate(string? stored) =>
        stored == "无" ? UnrecognizedPlateDisplay : stored ?? UnrecognizedPlateDisplay;
}
