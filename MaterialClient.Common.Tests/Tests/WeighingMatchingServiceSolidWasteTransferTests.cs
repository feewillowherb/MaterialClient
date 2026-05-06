using System.Reflection;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class WeighingMatchingServiceSolidWasteTransferTests
{
    private static MethodInfo GetCopyMethod()
    {
        var method = typeof(WeighingMatchingService).GetMethod(
            "CopySolidWasteInfoToWaybill",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.ShouldNotBeNull();
        return method!;
    }

    [Fact]
    public void CopySolidWasteInfoToWaybill_JoinFirst_FallbackToOutRecordForMissingFields()
    {
        // Arrange
        var join = new WeighingRecord(15m) { WeighingMode = WeighingMode.SolidWaste };
        var outRecord = new WeighingRecord(10m) { WeighingMode = WeighingMode.SolidWaste };

        join.SetSolidWasteType("TypeFromJoin");
        // join: no shipper set

        // out: provides shipper + missing fields
        outRecord.SetProperty("SolidWasteInfo.Shipper", "ShipperFromOut");

        var waybill = new Waybill(1, "test")
        {
            WeighingMode = WeighingMode.Standard,
            OrderGoodsWeight = 5.5m
        };

        var method = GetCopyMethod();

        // Act
        method.Invoke(null, new object[] { waybill, join, outRecord });

        // Assert
        waybill.WeighingMode.ShouldBe(WeighingMode.SolidWaste);
        waybill.GetSolidWasteType().ShouldBe("TypeFromJoin");
        waybill.GetSolidWasteShipper().ShouldBe("ShipperFromOut");
    }

    [Fact]
    public void CopySolidWasteInfoToWaybill_OutRecordUsedWhenJoinIsNotSolidWaste()
    {
        // Arrange
        var join = new WeighingRecord(15m) { WeighingMode = WeighingMode.Standard };
        var outRecord = new WeighingRecord(10m) { WeighingMode = WeighingMode.SolidWaste };

        outRecord.SetSolidWasteType("TypeFromOut");
        outRecord.SetSolidWasteStreet("StreetFromOut");
        outRecord.SetSolidWasteOrderNumber("OrderNoFromOut");

        var waybill = new Waybill(1, "test")
        {
            WeighingMode = WeighingMode.Standard,
            OrderGoodsWeight = 3.0m
        };

        var method = GetCopyMethod();

        // Act
        method.Invoke(null, new object[] { waybill, join, outRecord });

        // Assert
        waybill.WeighingMode.ShouldBe(WeighingMode.SolidWaste);
        waybill.GetSolidWasteType().ShouldBe("TypeFromOut");
        waybill.GetSolidWasteStreet().ShouldBe("StreetFromOut");
        waybill.GetSolidWasteOrderNumber().ShouldBe("OrderNoFromOut");
    }
}

