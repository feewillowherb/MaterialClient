using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class SolidWasteExcelExportTests
{
    #region 5.1 映射逻辑测试

    [Fact]
    public void MapToExportRow_NormalMapping_AllFieldsCorrect()
    {
        var waybill = CreateTestWaybill();
        waybill.SetProperty("SolidWasteInfo.MaterialId", 10);
        waybill.SetProperty("SolidWasteInfo.Street", "瓜沥镇");
        waybill.SetProperty("SolidWasteInfo.SolidWasteType", "村、社区");
        waybill.SetProperty("SolidWasteInfo.SolidWasteOrderNumber", "2414822");
        waybill.SetProperty("SolidWasteInfo.Shipper", "固废资源化综合体");

        var providerDict = new Dictionary<int, string> { { 1, "长巷村" } };
        var materialDict = new Dictionary<int, string> { { 10, "装修垃圾" } };

        var row = SolidWasteService.SolidWasteMapToExportRow(waybill, providerDict, materialDict);

        row.SerialNumber.ShouldBe("A202603040001");
        row.VehicleNumber.ShouldBe("浙A96H93");
        row.ShippingUnit.ShouldBe("长巷村");
        row.ReceivingUnit.ShouldBe("固废资源化综合体");
        row.GoodsName.ShouldBe("装修垃圾");
        row.GrossWeight.ShouldBe(8270m);
        row.TareWeight.ShouldBe(5750m);
        row.NetWeight.ShouldBe(2520m);
        row.Remark.ShouldBe("隋建国");
        row.GrossWeightTime.ShouldBe("2026-03-04 08:10:16");
        row.TareWeightTime.ShouldBe("2026-03-04 08:14:15");
        row.Street.ShouldBe("瓜沥镇");
        row.SolidWasteType.ShouldBe("村、社区");
        row.ManifestNumber.ShouldBe("2414822");
    }

    [Fact]
    public void MapToExportRow_UploadColumns_WhenNotPendingSync()
    {
        var waybill = CreateTestWaybill();
        waybill.IsPendingSync = false;
        waybill.LastSyncTime = null;
        var row = SolidWasteService.SolidWasteMapToExportRow(
            waybill, new Dictionary<int, string>(), new Dictionary<int, string>());

        row.UploadResult.ShouldBe("1");
        row.UploadStatus.ShouldBe("上传成功");
        row.UploadTime.ShouldBe(string.Empty);
    }

    [Fact]
    public void MapToExportRow_NullFields_ReturnsEmptyStrings()
    {
        var waybill = new Waybill(1, "TEST001")
        {
            WeighingMode = WeighingMode.SolidWaste,
            OrderType = OrderTypeEnum.Completed,
            PlateNumber = null,
            Remark = null,
            JoinTime = null,
            OutTime = null,
            AddDate = DateTime.Now
        };

        var row = SolidWasteService.SolidWasteMapToExportRow(
            waybill, new Dictionary<int, string>(), new Dictionary<int, string>());

        row.VehicleNumber.ShouldBe(string.Empty);
        row.Remark.ShouldBe(string.Empty);
        row.GrossWeightTime.ShouldBe(string.Empty);
        row.TareWeightTime.ShouldBe(string.Empty);
        row.Street.ShouldBe(string.Empty);
        row.SolidWasteType.ShouldBe(string.Empty);
        row.ManifestNumber.ShouldBe(string.Empty);
    }

    [Fact]
    public void MapToExportRow_MissingProvider_ReturnsEmptyShippingUnit()
    {
        var waybill = CreateTestWaybill();
        waybill.ProviderId = 999;

        var providerDict = new Dictionary<int, string> { { 1, "长巷村" } };
        var row = SolidWasteService.SolidWasteMapToExportRow(
            waybill, providerDict, new Dictionary<int, string>());

        row.ShippingUnit.ShouldBe(string.Empty);
    }

    [Fact]
    public void MapToExportRow_MissingMaterial_ReturnsEmptyGoodsName()
    {
        var waybill = CreateTestWaybill();
        waybill.SetProperty("SolidWasteInfo.MaterialId", 999);

        var materialDict = new Dictionary<int, string> { { 10, "装修垃圾" } };
        var row = SolidWasteService.SolidWasteMapToExportRow(
            waybill, new Dictionary<int, string>(), materialDict);

        row.GoodsName.ShouldBe(string.Empty);
    }

    #endregion

    private static Waybill CreateTestWaybill()
    {
        return new Waybill(1, "A202603040001")
        {
            WeighingMode = WeighingMode.SolidWaste,
            OrderType = OrderTypeEnum.Completed,
            ProviderId = 1,
            PlateNumber = "浙A96H93",
            OrderTotalWeight = 8270m,
            OrderTruckWeight = 5750m,
            OrderGoodsWeight = 2520m,
            Remark = "隋建国",
            JoinTime = new DateTime(2026, 3, 4, 8, 10, 16),
            OutTime = new DateTime(2026, 3, 4, 8, 14, 15),
            AddDate = new DateTime(2026, 3, 4)
        };
    }
}
