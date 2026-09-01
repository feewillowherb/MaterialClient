using MaterialClient.Common.Dtos.Urban;
using MaterialClient.Common.Entities.Enums;
using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities.Urban;

public class UrbanPassageRecord : Entity<Guid>
{
    public const string UnrecognizedPlateStored = "无";
    public const string DefaultPlateColor = "无";
    public const string DefaultVehicleType = "大车";

    protected UrbanPassageRecord()
    {
    }

    public PassageSource PassageSource { get; private set; }

    public string PlateNumber { get; private set; } = UnrecognizedPlateStored;

    public string PlateColor { get; private set; } = DefaultPlateColor;

    public string VehicleType { get; private set; } = DefaultVehicleType;

    public DateTime CapturedAt { get; private set; }

    public UrbanInOutType UrbanInOutType { get; private set; }

    public UrbanSiteType UrbanSiteType { get; private set; }

    public int? LargeImageAttachmentId { get; private set; }

    public int? SmallImageAttachmentId { get; private set; }

    public SyncStatus SyncStatus { get; private set; } = SyncStatus.Pending;

    public int RetryCount { get; private set; }

    public DateTime? LastErrorTime { get; private set; }

    public string? SubmitMachineCode { get; private set; }

    public void AssignSubmitMachineCode(string submitMachineCode)
    {
        SubmitMachineCode = submitMachineCode;
    }

    public void MarkSynced()
    {
        SyncStatus = SyncStatus.Synced;
        LastErrorTime = null;
    }

    public void MarkUploadFailed()
    {
        RetryCount++;
        LastErrorTime = DateTime.UtcNow;
    }

    public static UrbanPassageRecord FromLprCapture(UrbanLprCaptureContext context)
    {
        var plate = string.IsNullOrWhiteSpace(context.PlateNumber)
            ? UnrecognizedPlateStored
            : context.PlateNumber.Trim();
        var color = string.IsNullOrWhiteSpace(context.PlateColor)
            ? DefaultPlateColor
            : context.PlateColor.Trim();
        var vehicle = string.IsNullOrWhiteSpace(context.VehicleType)
            ? DefaultVehicleType
            : context.VehicleType.Trim();

        return new UrbanPassageRecord
        {
            Id = Guid.NewGuid(),
            PassageSource = context.Source,
            PlateNumber = plate,
            PlateColor = color,
            VehicleType = vehicle,
            CapturedAt = context.CapturedAt == default ? DateTime.Now : context.CapturedAt,
            UrbanInOutType = context.InOutType,
            UrbanSiteType = context.SiteType,
            LargeImageAttachmentId = context.LargeImageAttachmentId,
            SmallImageAttachmentId = context.SmallImageAttachmentId
        };
    }
}
