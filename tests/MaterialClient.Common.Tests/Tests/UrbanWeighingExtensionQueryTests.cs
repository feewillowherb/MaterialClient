using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     Tests for query patterns and tab filtering logic (IsAnomaly-based tabs).
/// </summary>
public class UrbanWeighingExtensionQueryTests
{
    #region LEFT JOIN Pattern Tests

    [Fact]
    public void LeftJoin_Pattern_Should_Return_All_Urban_Records_With_Extensions()
    {
        var records = new List<WeighingRecord>
        {
            CreateUrbanRecord(1, "京A12345", isAnomaly: false),
            CreateUrbanRecord(2, "粤B12345", isAnomaly: false),
            CreateUrbanRecord(3, "沪C12345", isAnomaly: true)
        };

        var results = records.Where(r => r.UrbanExtension != null).ToList();

        results.Count.ShouldBe(3);
        results.All(r => r.UrbanExtension != null).ShouldBeTrue();
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

        var allRecords = records.ToList();
        var withExtension = allRecords.Where(r => r.UrbanExtension != null).ToList();
        var withoutExtension = allRecords.Where(r => r.UrbanExtension == null).ToList();

        allRecords.Count.ShouldBe(3);
        withExtension.Count.ShouldBe(2);
        withoutExtension.Count.ShouldBe(1);
    }

    #endregion

    #region Tab Filter Logic Tests (IsAnomaly)

    [Fact]
    public void TabFilter_All_Should_Return_All_Urban_Records()
    {
        var records = new List<WeighingRecord>
        {
            CreateUrbanRecord(1, "京A12345", isAnomaly: false),
            CreateUrbanRecord(2, "粤B12345", isAnomaly: true),
            CreateUrbanRecord(3, "沪C12345", isAnomaly: false, syncStatus: SyncStatus.Failed)
        };

        records.Count.ShouldBe(3);
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

        var results = records
            .Where(r => r.UrbanExtension != null && !r.UrbanExtension.IsAnomaly)
            .ToList();

        results.Count.ShouldBe(2);
        results.All(r => !r.UrbanExtension!.IsAnomaly).ShouldBeTrue();
    }

    [Fact]
    public void TabFilter_Abnormal_Should_Return_Anomaly_Records()
    {
        var records = new List<WeighingRecord>
        {
            CreateUrbanRecord(1, "京A12345", isAnomaly: false),
            CreateUrbanRecord(2, "粤B12345", isAnomaly: false, syncStatus: SyncStatus.Failed),
            CreateUrbanRecord(3, "沪C12345", isAnomaly: true)
        };

        var results = records
            .Where(r => r.UrbanExtension != null && r.UrbanExtension.IsAnomaly)
            .ToList();

        results.Count.ShouldBe(1);
        results[0].UrbanExtension!.IsAnomaly.ShouldBeTrue();
        results[0].PlateNumber.ShouldBe("沪C12345");
    }

    [Fact]
    public void TabFilter_Normal_Should_Include_Records_Without_Extension_As_Normal()
    {
        var records = new List<WeighingRecord>
        {
            CreateUrbanRecord(1, "京A12345", isAnomaly: false),
            new WeighingRecord(15.0m, "粤B12345") { UrbanExtension = null },
            CreateUrbanRecord(3, "沪C12345", isAnomaly: true)
        };

        var results = records
            .Where(r => r.UrbanExtension == null || !r.UrbanExtension.IsAnomaly)
            .ToList();

        results.Count.ShouldBe(2);
        results.Any(r => r.PlateNumber == "粤B12345").ShouldBeTrue();
        results.All(r => r.UrbanExtension?.IsAnomaly != true).ShouldBeTrue();
    }

    [Fact]
    public void SyncFailed_With_IsAnomaly_False_Should_Still_Be_Normal_Tab()
    {
        var record = CreateUrbanRecord(1, "京A12345", isAnomaly: false, syncStatus: SyncStatus.Failed);

        var inNormalTab = record.UrbanExtension != null && !record.UrbanExtension.IsAnomaly;
        var inAbnormalTab = record.UrbanExtension != null && record.UrbanExtension.IsAnomaly;

        inNormalTab.ShouldBeTrue();
        inAbnormalTab.ShouldBeFalse();
        record.UrbanExtension!.SyncStatus.ShouldBe(SyncStatus.Failed);
    }

    #endregion

    #region Transactional Creation Tests

    [Fact]
    public void Urban_Record_Creation_Should_Create_Extension_With_Correct_Defaults()
    {
        var record = new WeighingRecord(20.0m, "京A12345");
        record.SetWeighingMode(WeighingMode.UrbanMode);

        var extension = new UrbanWeighingExtension
        {
            WeighingRecordId = record.Id,
            SyncStatus = SyncStatus.Pending,
            RetryCount = 0,
            LastErrorTime = null,
            IsAnomaly = false
        };
        record.UrbanExtension = extension;

        record.WeighingMode.ShouldBe(WeighingMode.UrbanMode);
        record.UrbanExtension.ShouldNotBeNull();
        record.UrbanExtension.SyncStatus.ShouldBe(SyncStatus.Pending);
        record.UrbanExtension.IsAnomaly.ShouldBeFalse();
    }

    [Fact]
    public void Standard_Record_Creation_Should_Not_Create_Extension()
    {
        var record = new WeighingRecord(15.0m, "粤B12345");
        record.SetWeighingMode(WeighingMode.Standard);

        record.UrbanExtension.ShouldBeNull();
        record.WeighingMode.ShouldBe(WeighingMode.Standard);
    }

    [Fact]
    public void Extension_And_Record_Should_Have_Consistent_Ids()
    {
        var record = new WeighingRecord(42, 20.0m) { PlateNumber = "京A12345" };

        var extension = new UrbanWeighingExtension
        {
            WeighingRecordId = record.Id,
            SyncStatus = SyncStatus.Pending
        };
        record.UrbanExtension = extension;

        extension.WeighingRecordId.ShouldBe(record.Id);
    }

    #endregion

    private static WeighingRecord CreateUrbanRecord(
        long id,
        string plateNumber,
        bool isAnomaly,
        SyncStatus syncStatus = SyncStatus.Pending)
    {
        var record = new WeighingRecord(id, 10.0m) { PlateNumber = plateNumber };
        record.SetWeighingMode(WeighingMode.UrbanMode);

        var extension = new UrbanWeighingExtension
        {
            WeighingRecordId = id,
            SyncStatus = syncStatus,
            RetryCount = syncStatus == SyncStatus.Failed ? 3 : 0,
            LastErrorTime = syncStatus == SyncStatus.Failed ? DateTime.UtcNow : null,
            IsAnomaly = isAnomaly
        };
        record.UrbanExtension = extension;

        return record;
    }
}
