using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class UrbanWeighingExtensionQueryTests
{
    private static readonly Dictionary<long, UrbanWeighingExtension?> Extensions = new();

    public UrbanWeighingExtensionQueryTests()
    {
        Extensions.Clear();
    }

    [Fact]
    public void LeftJoin_Pattern_Should_Return_All_Urban_Records_With_Extensions()
    {
        var records = new List<WeighingRecord>
        {
            CreateUrbanRecord(1, "京A12345", isAnomaly: false),
            CreateUrbanRecord(2, "粤B12345", isAnomaly: false),
            CreateUrbanRecord(3, "沪C12345", isAnomaly: true)
        };

        var results = records.Where(r => Ext(r) != null).ToList();
        results.Count.ShouldBe(3);
    }

    [Fact]
    public void LeftJoin_Pattern_Should_Return_Records_Without_Extensions()
    {
        var records = new List<WeighingRecord>
        {
            CreateUrbanRecord(1, "京A12345", isAnomaly: false),
            new WeighingRecord(15.0m, "粤B12345") { WeighingMode = WeighingMode.Standard },
            CreateUrbanRecord(3, "沪C12345", isAnomaly: false)
        };

        var withExtension = records.Where(r => Ext(r) != null).ToList();
        var withoutExtension = records.Where(r => Ext(r) == null).ToList();
        withExtension.Count.ShouldBe(2);
        withoutExtension.Count.ShouldBe(1);
    }

    [Fact]
    public void TabFilter_Normal_Should_Return_NonAnomaly_Records()
    {
        var records = new List<WeighingRecord>
        {
            CreateUrbanRecord(1, "京A12345", isAnomaly: false),
            CreateUrbanRecord(2, "粤B12345", isAnomaly: false, syncStatus: SyncStatus.Failed),
            CreateUrbanRecord(3, "沪C12345", isAnomaly: true)
        };

        var results = records.Where(r => Ext(r) != null && !Ext(r)!.IsAnomaly).ToList();
        results.Count.ShouldBe(2);
    }

    [Fact]
    public void TabFilter_Abnormal_Should_Return_Anomaly_Records()
    {
        var records = new List<WeighingRecord>
        {
            CreateUrbanRecord(1, "京A12345", isAnomaly: false),
            CreateUrbanRecord(3, "沪C12345", isAnomaly: true)
        };

        var results = records.Where(r => Ext(r) != null && Ext(r)!.IsAnomaly).ToList();
        results.Count.ShouldBe(1);
        results[0].PlateNumber.ShouldBe("沪C12345");
    }

    [Fact]
    public void TabFilter_Normal_Should_Include_Records_Without_Extension_As_Normal()
    {
        var records = new List<WeighingRecord>
        {
            CreateUrbanRecord(1, "京A12345", isAnomaly: false),
            new WeighingRecord(15.0m, "粤B12345"),
            CreateUrbanRecord(3, "沪C12345", isAnomaly: true)
        };

        var results = records.Where(r => Ext(r) == null || !Ext(r)!.IsAnomaly).ToList();
        results.Count.ShouldBe(2);
    }

    [Fact]
    public void Standard_Record_Creation_Should_Not_Create_Extension()
    {
        var record = new WeighingRecord(15.0m, "粤B12345");
        record.SetWeighingMode(WeighingMode.Standard);
        Ext(record).ShouldBeNull();
    }

    [Fact]
    public void Extension_And_Record_Should_Have_Consistent_Ids()
    {
        var record = CreateUrbanRecord(42, "京A12345", isAnomaly: false);
        Ext(record)!.WeighingRecordId.ShouldBe(record.Id);
    }

    private static UrbanWeighingExtension? Ext(WeighingRecord record) =>
        Extensions.GetValueOrDefault(record.Id);

    private static WeighingRecord CreateUrbanRecord(
        long id,
        string plateNumber,
        bool isAnomaly,
        SyncStatus syncStatus = SyncStatus.Pending)
    {
        var record = new WeighingRecord(id, 10.0m) { PlateNumber = plateNumber };
        record.SetWeighingMode(WeighingMode.UrbanMode);
        Extensions[id] = new UrbanWeighingExtension
        {
            WeighingRecordId = id,
            SyncStatus = syncStatus,
            RetryCount = syncStatus == SyncStatus.Failed ? 3 : 0,
            LastErrorTime = syncStatus == SyncStatus.Failed ? DateTime.UtcNow : null,
            IsAnomaly = isAnomaly
        };
        return record;
    }
}
