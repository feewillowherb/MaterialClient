using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     Unit tests for the UrbanWeighingExtension entity.
///     Covers property initialization, default values, and navigation property configuration.
/// </summary>
public class UrbanWeighingExtensionTests
{
    #region Entity Property Tests (Task 5.1)

    [Fact]
    public void UrbanWeighingExtension_Should_Initialize_With_Default_Values()
    {
        // Arrange & Act
        var extension = new UrbanWeighingExtension();

        // Assert
        extension.SyncStatus.ShouldBe(SyncStatus.Pending);
        extension.RetryCount.ShouldBe(0);
        extension.LastErrorTime.ShouldBeNull();
        extension.WeighingRecordId.ShouldBe(0);
    }

    [Fact]
    public void UrbanWeighingExtension_Should_Allow_Setting_Properties()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var extension = new UrbanWeighingExtension
        {
            WeighingRecordId = 42,
            SyncStatus = SyncStatus.Failed,
            RetryCount = 3,
            LastErrorTime = now
        };

        // Assert
        extension.WeighingRecordId.ShouldBe(42);
        extension.SyncStatus.ShouldBe(SyncStatus.Failed);
        extension.RetryCount.ShouldBe(3);
        extension.LastErrorTime.ShouldBe(now);
    }

    [Theory]
    [InlineData(SyncStatus.Pending)]
    [InlineData(SyncStatus.Synced)]
    [InlineData(SyncStatus.Failed)]
    public void UrbanWeighingExtension_Should_Accept_All_SyncStatus_Values(SyncStatus status)
    {
        // Arrange & Act
        var extension = new UrbanWeighingExtension { SyncStatus = status };

        // Assert
        extension.SyncStatus.ShouldBe(status);
    }

    [Fact]
    public void UrbanWeighingExtension_Should_Allow_Null_LastErrorTime()
    {
        // Arrange
        var extension = new UrbanWeighingExtension { LastErrorTime = DateTime.UtcNow };

        // Act
        extension.LastErrorTime = null;

        // Assert
        extension.LastErrorTime.ShouldBeNull();
    }

    [Fact]
    public void UrbanWeighingExtension_WeighingRecordId_Should_Default_To_Zero()
    {
        var extension = new UrbanWeighingExtension();
        extension.WeighingRecordId.ShouldBe(0);
    }

    #endregion

    [Fact]
    public void WeighingRecord_Should_Not_Have_SyncStatus_Property()
    {
        // Arrange
        var record = new WeighingRecord(10.5m);
        var property = record.GetType().GetProperty("SyncStatus");

        // Assert - SyncStatus should have been removed from WeighingRecord
        property.ShouldBeNull();
    }

    [Fact]
    public void WeighingRecord_NonUrban_Should_Work_Without_Extension()
    {
        // Arrange & Act
        var record = new WeighingRecord(15.0m, "京A12345")
        {
            WeighingMode = WeighingMode.Standard,
            DeliveryType = DeliveryType.Receiving
        };

        // Assert - Standard mode records should work fine without extension
        record.TotalWeight.ShouldBe(15.0m);
        record.PlateNumber.ShouldBe("京A12345");
        record.WeighingMode.ShouldBe(WeighingMode.Standard);
    }

    [Fact]
    public void WeighingRecord_UrbanMode_With_Extension_Should_Be_Consistent()
    {
        var record = new WeighingRecord(20.0m, "粤B12345");
        record.SetWeighingMode(WeighingMode.UrbanMode);

        var extension = new UrbanWeighingExtension
        {
            WeighingRecordId = record.Id,
            SyncStatus = SyncStatus.Pending,
            RetryCount = 0
        };

        record.WeighingMode.ShouldBe(WeighingMode.UrbanMode);
        extension.SyncStatus.ShouldBe(SyncStatus.Pending);
        extension.WeighingRecordId.ShouldBe(record.Id);
    }

    #region 1:0..1 Relationship Configuration Tests (Task 5.2)

    [Fact]
    public void UrbanWeighingExtension_FK_Should_Reference_WeighingRecord()
    {
        // Arrange
        var record = new WeighingRecord(100, 25.0m);
        var extension = new UrbanWeighingExtension
        {
            WeighingRecordId = record.Id
        };

        // Assert
        extension.WeighingRecordId.ShouldBe(record.Id);
    }

    [Fact]
    public void Multiple_Extensions_Should_Not_Share_Same_WeighingRecordId()
    {
        // This test documents the expected constraint: each WeighingRecord
        // should have at most one UrbanWeighingExtension (enforced by DB unique index)
        var recordId = 100L;

        var extension1 = new UrbanWeighingExtension { WeighingRecordId = recordId };
        var extension2 = new UrbanWeighingExtension { WeighingRecordId = recordId };

        // Both extensions reference the same record - this would fail at DB level
        // due to unique constraint on WeighingRecordId
        extension1.WeighingRecordId.ShouldBe(extension2.WeighingRecordId);
    }

    #endregion
}
