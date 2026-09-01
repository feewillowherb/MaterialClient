using MaterialClient.Common.Dtos.Urban;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using MaterialClient.Urban.Dtos;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class UrbanPassageSubmitDtoTests
{
    [Fact]
    public void FromPassage_Checkpoint_MapsClientRecordIdAndFields()
    {
        var capturedAt = new DateTime(2026, 9, 1, 12, 0, 0);
        var record = UrbanPassageRecord.FromLprCapture(new UrbanLprCaptureContext(
            "浙A12345",
            "黄",
            "大车",
            capturedAt,
            PassageSource.Checkpoint,
            UrbanInOutType.Enter,
            UrbanSiteType.Construction,
            1,
            null));

        var license = new LicenseInfo(
            Guid.NewGuid(),
            Guid.Parse("08DDCF46-3744-D3E1-1999-0D645800B322"),
            DateTime.UtcNow.AddYears(1),
            "machine-1",
            "demo",
            "XNXS20260611001");

        var dto = UrbanPassageSubmitDto.FromPassage(record, license, "machine-1", [Guid.NewGuid()]);

        Assert.Equal(record.Id, dto.ClientRecordId);
        Assert.Equal("浙A12345", dto.PlateNumber);
        Assert.Equal(UrbanInOutType.Enter, dto.UrbanInOutType);
        Assert.Equal(license.ProjectId, dto.ProId);
        Assert.Equal(license.AccessCode, dto.BuildLicenseNo);
        Assert.Single(dto.AttachmentIds!);
    }

    [Fact]
    public void FromPassage_FinishedProduct_PreservesSourceFields()
    {
        var record = UrbanPassageRecord.FromLprCapture(new UrbanLprCaptureContext(
            "浙B88888",
            "蓝",
            "小车",
            DateTime.Now,
            PassageSource.FinishedProduct,
            UrbanInOutType.Exit,
            UrbanSiteType.Disposal,
            null,
            null));

        var dto = UrbanPassageSubmitDto.FromPassage(record, null, "machine-2", []);

        Assert.Equal(record.Id, dto.ClientRecordId);
        Assert.Equal(UrbanSiteType.Disposal, dto.UrbanSiteType);
        Assert.Null(dto.AttachmentIds);
    }
}
