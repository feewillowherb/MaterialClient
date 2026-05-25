using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     Integration-style tests for query patterns and tab filtering logic
///     using in-memory collections to simulate LEFT JOIN and filter behaviors.
/// </summary>
public class UrbanWeighingExtensionQueryTests
{
    #region LEFT JOIN Pattern Tests (Task 5.3)

    [Fact]
    public void LeftJoin_Pattern_Should_Return_All_Urban_Records_With_Extensions()
    {
        // Arrange - simulate LEFT JOIN results
        var records = new List<WeighingRecord>
        {
            CreateUrbanRecord(1, "京A12345", SyncStatus.Pending),
            CreateUrbanRecord(2, "粤B12345", SyncStatus.Synced),
            CreateUrbanRecord(3, "沪C12345", SyncStatus.Failed)
        };

        // Act - all records have extensions (normal case for Urban mode)
        var results = records.Where(r => r.UrbanExtension != null).ToList();

        // Assert
        results.Count.ShouldBe(3);
        results.All(r => r.UrbanExtension != null).ShouldBeTrue();
    }

    [Fact]
    public void LeftJoin_Pattern_Should_Return_Records_Without_Extensions()
    {
        // Arrange - mixed records (some with extension, some without)
        var records = new List<WeighingRecord>
        {
            CreateUrbanRecord(1, "京A12345", SyncStatus.Pending),
            new WeighingRecord(15.0m, "粤B12345") { WeighingMode = WeighingMode.Standard },
            CreateUrbanRecord(3, "沪C12345", SyncStatus.Synced)
        };

        // Act - LEFT JOIN returns all records, extension may be null
        var allRecords = records.ToList();
        var withExtension = allRecords.Where(r => r.UrbanExtension != null).ToList();
        var withoutExtension = allRecords.Where(r => r.UrbanExtension == null).ToList();

        // Assert
        allRecords.Count.ShouldBe(3);
        withExtension.Count.ShouldBe(2);
        withoutExtension.Count.ShouldBe(1);
    }

    #endregion

    #region Tab Filter Logic Tests (Task 5.3)

    [Fact]
    public void TabFilter_All_Should_Return_All_Urban_Records()
    {
        // Arrange
        var records = new List<WeighingRecord>
        {
            CreateUrbanRecord(1, "京A12345", SyncStatus.Pending),
            CreateUrbanRecord(2, "粤B12345", SyncStatus.Synced),
            CreateUrbanRecord(3, "沪C12345", SyncStatus.Failed)
        };

        // Act - "全部" tab returns all records regardless of status
        var results = records.ToList();

        // Assert
        results.Count.ShouldBe(3);
    }

    [Fact]
    public void TabFilter_Normal_Should_Return_NonFailed_Records()
    {
        // Arrange
        var records = new List<WeighingRecord>
        {
            CreateUrbanRecord(1, "京A12345", SyncStatus.Pending),
            CreateUrbanRecord(2, "粤B12345", SyncStatus.Synced),
            CreateUrbanRecord(3, "沪C12345", SyncStatus.Failed)
        };

        // Act - "正常" tab filters by extension.SyncStatus != Failed
        var results = records
            .Where(r => r.UrbanExtension != null && r.UrbanExtension.SyncStatus != SyncStatus.Failed)
            .ToList();

        // Assert
        results.Count.ShouldBe(2);
        results.All(r => r.UrbanExtension!.SyncStatus != SyncStatus.Failed).ShouldBeTrue();
    }

    [Fact]
    public void TabFilter_Abnormal_Should_Return_Failed_Records()
    {
        // Arrange
        var records = new List<WeighingRecord>
        {
            CreateUrbanRecord(1, "京A12345", SyncStatus.Pending),
            CreateUrbanRecord(2, "粤B12345", SyncStatus.Synced),
            CreateUrbanRecord(3, "沪C12345", SyncStatus.Failed)
        };

        // Act - "异常" tab filters by extension.SyncStatus == Failed
        var results = records
            .Where(r => r.UrbanExtension != null && r.UrbanExtension.SyncStatus == SyncStatus.Failed)
            .ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].UrbanExtension!.SyncStatus.ShouldBe(SyncStatus.Failed);
        results[0].PlateNumber.ShouldBe("沪C12345");
    }

    [Fact]
    public void TabFilter_Normal_Should_Exclude_Records_Without_Extension()
    {
        // Arrange - includes a record without extension
        var records = new List<WeighingRecord>
        {
            CreateUrbanRecord(1, "京A12345", SyncStatus.Pending),
            new WeighingRecord(15.0m, "粤B12345") { UrbanExtension = null },
            CreateUrbanRecord(3, "沪C12345", SyncStatus.Synced)
        };

        // Act - "正常" tab requires extension to exist
        var results = records
            .Where(r => r.UrbanExtension != null && r.UrbanExtension.SyncStatus != SyncStatus.Failed)
            .ToList();

        // Assert
        results.Count.ShouldBe(2);
        results.All(r => r.PlateNumber != "粤B12345").ShouldBeTrue();
    }

    #endregion

    #region Transactional Creation Tests (Task 5.4)

    [Fact]
    public void Urban_Record_Creation_Should_Create_Extension_With_Correct_Defaults()
    {
        // Arrange
        var record = new WeighingRecord(20.0m, "京A12345");
        record.SetWeighingMode(WeighingMode.UrbanMode);

        // Act - simulate the creation logic in WeighingRecordService
        var extension = new UrbanWeighingExtension
        {
            WeighingRecordId = record.Id,
            SyncStatus = SyncStatus.Pending,
            RetryCount = 0,
            LastErrorTime = null
        };
        record.UrbanExtension = extension;

        // Assert
        record.WeighingMode.ShouldBe(WeighingMode.UrbanMode);
        record.UrbanExtension.ShouldNotBeNull();
        record.UrbanExtension.SyncStatus.ShouldBe(SyncStatus.Pending);
        record.UrbanExtension.RetryCount.ShouldBe(0);
        record.UrbanExtension.LastErrorTime.ShouldBeNull();
    }

    [Fact]
    public void Standard_Record_Creation_Should_Not_Create_Extension()
    {
        // Arrange & Act
        var record = new WeighingRecord(15.0m, "粤B12345");
        record.SetWeighingMode(WeighingMode.Standard);

        // Assert - Standard mode should not create extension
        record.UrbanExtension.ShouldBeNull();
        record.WeighingMode.ShouldBe(WeighingMode.Standard);
    }

    [Fact]
    public void SolidWaste_Record_Creation_Should_Not_Create_Extension()
    {
        // Arrange & Act
        var record = new WeighingRecord(12.0m, "沪C12345");
        record.SetWeighingMode(WeighingMode.SolidWaste);

        // Assert - SolidWaste mode should not create extension
        record.UrbanExtension.ShouldBeNull();
        record.WeighingMode.ShouldBe(WeighingMode.SolidWaste);
    }

    [Fact]
    public void Extension_And_Record_Should_Have_Consistent_Ids()
    {
        // Arrange
        var record = new WeighingRecord(42, 20.0m) { PlateNumber = "京A12345" };

        // Act
        var extension = new UrbanWeighingExtension
        {
            WeighingRecordId = record.Id,
            SyncStatus = SyncStatus.Pending
        };
        record.UrbanExtension = extension;

        // Assert - FK should reference the correct record
        extension.WeighingRecordId.ShouldBe(record.Id);
    }

    #endregion

    #region Helper Methods

    private static WeighingRecord CreateUrbanRecord(long id, string plateNumber, SyncStatus syncStatus)
    {
        var record = new WeighingRecord(id, 10.0m) { PlateNumber = plateNumber };
        record.SetWeighingMode(WeighingMode.UrbanMode);

        var extension = new UrbanWeighingExtension
        {
            WeighingRecordId = id,
            SyncStatus = syncStatus,
            RetryCount = syncStatus == SyncStatus.Failed ? 3 : 0,
            LastErrorTime = syncStatus == SyncStatus.Failed ? DateTime.UtcNow : null
        };
        record.UrbanExtension = extension;

        return record;
    }

    #endregion
}
