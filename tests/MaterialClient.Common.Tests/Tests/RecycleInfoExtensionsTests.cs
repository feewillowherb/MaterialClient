using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class RecycleInfoExtensionsTests
{
    [Fact]
    public void SetAndGet_UnitPrice_And_SaleContractNo()
    {
        var record = new WeighingRecord(10m);

        record.SetUnitPrice(120.5m);
        record.SetSaleContractNo("HT-001");

        record.GetUnitPrice().ShouldBe(120.5m);
        record.GetSaleContractNo().ShouldBe("HT-001");
    }

    [Fact]
    public void Set_Null_Clears_Values()
    {
        var record = new WeighingRecord(10m);
        record.SetRecycleInfo(99m, "OLD");

        record.SetUnitPrice(null);
        record.SetSaleContractNo(null);

        record.GetUnitPrice().ShouldBeNull();
        record.GetSaleContractNo().ShouldBeNull();
    }

    [Fact]
    public void SetRecycleInfo_Sets_Both_Fields()
    {
        var record = new WeighingRecord(10m);

        record.SetRecycleInfo(80m, "HT-BATCH");

        record.GetUnitPrice().ShouldBe(80m);
        record.GetSaleContractNo().ShouldBe("HT-BATCH");
    }

    [Fact]
    public void SetSaleContractNo_Whitespace_Becomes_Null()
    {
        var record = new WeighingRecord(10m);

        record.SetSaleContractNo("   ");

        record.GetSaleContractNo().ShouldBeNull();
    }

    [Fact]
    public void ResolveFromWeighingRecords_JoinFirst_FallbackToOut()
    {
        var join = new WeighingRecord(15m) { WeighingMode = WeighingMode.Recycle };
        var outRecord = new WeighingRecord(10m) { WeighingMode = WeighingMode.Recycle };

        join.SetUnitPrice(120m);
        outRecord.SetSaleContractNo("HT-FROM-OUT");

        var values = RecycleInfoExtensions.ResolveFromWeighingRecords(join, outRecord);

        values.ShouldNotBeNull();
        values!.UnitPrice.ShouldBe(120m);
        values.SaleContractNo.ShouldBe("HT-FROM-OUT");
        values.HasAnyValue.ShouldBeTrue();
    }

    [Fact]
    public void ResolveFromWeighingRecords_OutUsedWhenJoinNotRecycle()
    {
        var join = new WeighingRecord(15m) { WeighingMode = WeighingMode.Standard };
        var outRecord = new WeighingRecord(10m) { WeighingMode = WeighingMode.Recycle };
        outRecord.SetRecycleInfo(80m, "HT-OUT");

        var values = RecycleInfoExtensions.ResolveFromWeighingRecords(join, outRecord);

        values.ShouldNotBeNull();
        values!.UnitPrice.ShouldBe(80m);
        values.SaleContractNo.ShouldBe("HT-OUT");
    }

    [Fact]
    public void ResolveFromWeighingRecords_BothEmpty_HasAnyValueFalse()
    {
        var join = new WeighingRecord(15m) { WeighingMode = WeighingMode.Recycle };
        var outRecord = new WeighingRecord(10m) { WeighingMode = WeighingMode.Recycle };

        var values = RecycleInfoExtensions.ResolveFromWeighingRecords(join, outRecord);

        values.ShouldNotBeNull();
        values!.HasAnyValue.ShouldBeFalse();
    }

    [Fact]
    public void ResolveFromWeighingRecords_NonRecycle_ReturnsNull()
    {
        var join = new WeighingRecord(15m) { WeighingMode = WeighingMode.Standard };
        var outRecord = new WeighingRecord(10m) { WeighingMode = WeighingMode.SolidWaste };
        join.SetUnitPrice(1m);

        var values = RecycleInfoExtensions.ResolveFromWeighingRecords(join, outRecord);

        values.ShouldBeNull();
    }
}
