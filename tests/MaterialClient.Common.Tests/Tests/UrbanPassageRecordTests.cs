using MaterialClient.Common.Configuration;
using MaterialClient.Common.Dtos.Urban;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class UrbanPassageRecordTests
{
    [Fact]
    public void FromLprCapture_AppliesDefaults()
    {
        var record = UrbanPassageRecord.FromLprCapture(new UrbanLprCaptureContext(
            null,
            null,
            null,
            DateTime.Now,
            PassageSource.Checkpoint,
            UrbanInOutType.Exit,
            UrbanSiteType.Disposal,
            3,
            null));

        Assert.Equal("无", record.PlateNumber);
        Assert.Equal("无", record.PlateColor);
        Assert.Equal("大车", record.VehicleType);
        Assert.Equal(PassageSource.Checkpoint, record.PassageSource);
        Assert.Equal(UrbanInOutType.Exit, record.UrbanInOutType);
        Assert.Equal(3, record.LargeImageAttachmentId);
        Assert.Equal(SyncStatus.Pending, record.SyncStatus);
        Assert.Equal(0, record.RetryCount);
        Assert.Null(record.LastErrorTime);
    }

    [Fact]
    public void MarkSynced_UpdatesSyncStatus()
    {
        var record = UrbanPassageRecord.FromLprCapture(new UrbanLprCaptureContext(
            "浙A12345",
            "黄",
            "大车",
            DateTime.Now,
            PassageSource.FinishedProduct,
            UrbanInOutType.Enter,
            UrbanSiteType.Disposal,
            null,
            null));
        record.MarkUploadFailed();
        record.MarkSynced();
        Assert.Equal(SyncStatus.Synced, record.SyncStatus);
        Assert.Null(record.LastErrorTime);
    }

    [Fact]
    public void FormatStoredPlate_MapsUnrecognized()
    {
        Assert.Equal("未识别", UrbanAttendedListRow.FormatStoredPlate("无"));
        Assert.Equal("浙A12345", UrbanAttendedListRow.FormatStoredPlate("浙A12345"));
    }

    [Fact]
    public void UrbanLprCapabilities_FromConfigs()
    {
        var caps = UrbanLprCapabilities.FromConfigs(
        [
            new LicensePlateRecognitionConfig
            {
                Name = "a",
                Ip = "1.1.1.1",
                SiteType = LprSiteType.Checkpoint
            },
            new LicensePlateRecognitionConfig
            {
                Name = "b",
                Ip = "1.1.1.2",
                SiteType = LprSiteType.Scale
            }
        ]);

        Assert.True(caps.HasCheckpoint);
        Assert.True(caps.HasScale);
        Assert.False(caps.HasFinishedProduct);
    }

    [Fact]
    public void Json_OmitsInOut_DefaultsEnterAndConstruction()
    {
        const string json = """[{"Name":"old","Ip":"10.0.0.3","Direction":0,"SiteType":1}]""";
        var loaded = System.Text.Json.JsonSerializer.Deserialize<List<LicensePlateRecognitionConfig>>(json);
        Assert.NotNull(loaded);
        Assert.Equal(UrbanInOutType.Enter, loaded[0].UrbanInOutType);
        Assert.Equal(UrbanSiteType.Construction, loaded[0].UrbanSiteType);
        Assert.Equal(LprSiteType.Checkpoint, loaded[0].SiteType);
    }

    [Fact]
    public void FindByDeviceName_Matches()
    {
        var configs = new List<LicensePlateRecognitionConfig>
        {
            new() { Name = "gate1", Ip = "1.1.1.1", SiteType = LprSiteType.Checkpoint }
        };
        var found = LicensePlateRecognitionConfig.FindByDeviceName(configs, "gate1");
        Assert.NotNull(found);
        Assert.Equal(LprSiteType.Checkpoint, found.SiteType);
        Assert.Null(LicensePlateRecognitionConfig.FindByDeviceName(configs, "missing"));
    }
}
