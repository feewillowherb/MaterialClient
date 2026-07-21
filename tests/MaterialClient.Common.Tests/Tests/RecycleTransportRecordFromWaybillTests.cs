using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Recycle.Models;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     单测：RecycleTransportRecord.FromWaybill 五字段映射（含 null 分支）与 outPhotos 进场+出场顺序。
/// </summary>
public class RecycleTransportRecordFromWaybillTests
{
    private static Waybill BuildWaybill() =>
        new(1001, "fl-20260709103000-0001")
        {
            PlateNumber = "浙A12345",
            OrderGoodsWeight = 12.5m,
            OrderTruckWeight = 5.0m,
            OrderTotalWeight = 17.5m,
            OutTime = new DateTime(2026, 7, 9, 10, 30, 0),
            ProviderId = 200
        };

    [Fact]
    public void FromWaybill_Maps_All_Five_Fields_When_Provided()
    {
        var waybill = BuildWaybill();
        var receivingTime = new DateTime(2026, 7, 9, 15, 20, 0);

        var record = RecycleTransportRecord.FromWaybill(
            waybill,
            outPhotos: "entryBase64,exitBase64",
            productName: "成品灰土",
            pointNumber: "P-001",
            consignee: "测试运输公司",
            unitPrice: 120.0m,
            saleContractNo: "HT-2026-0001",
            receivingTime: receivingTime,
            receivingProof: "proofBase64",
            consigneeAddress: "杭州市西湖区某路 1 号");

        record.UnitPrice.ShouldBe(120.0m);
        record.SaleContractNo.ShouldBe("HT-2026-0001");
        record.ReceivingTime.ShouldBe("2026-07-09 15:20:00");
        record.ReceivingProof.ShouldBe("proofBase64");
        record.ConsigneeAddress.ShouldBe("杭州市西湖区某路 1 号");
    }

    [Fact]
    public void FromWaybill_Null_Fields_Become_Null()
    {
        var waybill = BuildWaybill();

        var record = RecycleTransportRecord.FromWaybill(
            waybill,
            outPhotos: "entryBase64",
            productName: "成品灰土",
            pointNumber: "P-001");

        // 可选字段未传时为 null（不上报该可选字段）
        record.UnitPrice.ShouldBeNull();
        record.SaleContractNo.ShouldBeNull();
        record.ReceivingTime.ShouldBeNull();
        record.ReceivingProof.ShouldBeNull();
        record.ConsigneeAddress.ShouldBeNull();
    }

    [Fact]
    public void FromWaybill_ReceivingTime_Formatted_As_yyyy_MM_dd_HH_mm_ss()
    {
        var waybill = BuildWaybill();
        var receivingTime = new DateTime(2026, 12, 31, 23, 59, 59);

        var record = RecycleTransportRecord.FromWaybill(
            waybill, "p", "m", "P", null, receivingTime: receivingTime);

        record.ReceivingTime.ShouldBe("2026-12-31 23:59:59");
    }

    [Fact]
    public void FromWaybill_Blank_Contract_And_Address_Become_Null()
    {
        var waybill = BuildWaybill();

        var record = RecycleTransportRecord.FromWaybill(
            waybill, "p", "m", "P", null,
            saleContractNo: "   ",
            consigneeAddress: "");

        record.SaleContractNo.ShouldBeNull();
        record.ConsigneeAddress.ShouldBeNull();
    }

    [Fact]
    public void FromWaybill_OutPhotos_Preserves_Entry_First_Exit_Second_Order()
    {
        var waybill = BuildWaybill();

        var record = RecycleTransportRecord.FromWaybill(
            waybill,
            outPhotos: "entry1,entry2,exit1,exit2",
            productName: "m",
            pointNumber: "P");

        // outPhotos 进场在前、出场在后，逗号分隔、无空格
        record.OutPhotos.ShouldBe("entry1,entry2,exit1,exit2");
        record.OutPhotos.ShouldNotContain(" ");
    }

    [Fact]
    public void ForLogging_Masks_OutPhotos_And_ReceivingProof()
    {
        var waybill = BuildWaybill();

        var record = RecycleTransportRecord.FromWaybill(
            waybill,
            outPhotos: "entry1,exit1",
            productName: "m",
            pointNumber: "P",
            receivingProof: "proofBase64");

        var safe = record.ForLogging();

        safe.OutPhotos.ShouldStartWith("[");
        safe.OutPhotos.ShouldEndWith("omitted]");
        safe.OutPhotos.ShouldNotContain("entry1");
        safe.ReceivingProof.ShouldStartWith("[");
        safe.ReceivingProof.ShouldEndWith("omitted]");
        safe.ReceivingProof.ShouldNotContain("proofBase64");
    }
}
