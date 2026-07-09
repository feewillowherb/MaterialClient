using System.Reflection;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class WeighingMatchingServiceRecycleWeighingModeTests
{
    private static MethodInfo GetApplyWeighingModeMethod()
    {
        var method = typeof(WeighingMatchingService).GetMethod(
            "ApplyWeighingModeFromRecordsIfNeeded",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.ShouldNotBeNull();
        return method!;
    }

    [Fact]
    public void ApplyWeighingModeFromRecordsIfNeeded_SetsRecycleWhenJoinRecordIsRecycle()
    {
        var join = new WeighingRecord(15m) { WeighingMode = WeighingMode.Recycle };
        var outRecord = new WeighingRecord(10m) { WeighingMode = WeighingMode.Recycle };
        var waybill = new Waybill(1, "test") { WeighingMode = WeighingMode.Standard };

        GetApplyWeighingModeMethod().Invoke(null, new object[] { waybill, join, outRecord });

        waybill.WeighingMode.ShouldBe(WeighingMode.Recycle);
    }

    [Fact]
    public void ApplyWeighingModeFromRecordsIfNeeded_SetsRecycleWhenOnlyOutRecordIsRecycle()
    {
        var join = new WeighingRecord(15m) { WeighingMode = WeighingMode.Standard };
        var outRecord = new WeighingRecord(10m) { WeighingMode = WeighingMode.Recycle };
        var waybill = new Waybill(1, "test") { WeighingMode = WeighingMode.Standard };

        GetApplyWeighingModeMethod().Invoke(null, new object[] { waybill, join, outRecord });

        waybill.WeighingMode.ShouldBe(WeighingMode.Recycle);
    }

    [Fact]
    public void ApplyWeighingModeFromRecordsIfNeeded_DoesNotOverrideSolidWaste()
    {
        var join = new WeighingRecord(15m) { WeighingMode = WeighingMode.SolidWaste };
        var outRecord = new WeighingRecord(10m) { WeighingMode = WeighingMode.SolidWaste };
        var waybill = new Waybill(1, "test") { WeighingMode = WeighingMode.SolidWaste };

        GetApplyWeighingModeMethod().Invoke(null, new object[] { waybill, join, outRecord });

        waybill.WeighingMode.ShouldBe(WeighingMode.SolidWaste);
    }
}
