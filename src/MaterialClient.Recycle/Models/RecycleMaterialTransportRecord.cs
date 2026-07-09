using System.Text.Json.Serialization;
using MaterialClient.Common.Entities;

namespace MaterialClient.Recycle.Models;

/// <summary>
///     资源化利用厂物料进场运输记录请求 DTO（§2.3 接口 materialTransportRecord/v1/addBatch）。
///     重量单位为「kg」；时间格式 yyyy-MM-dd HH:mm:ss；
///     inPhoto 为纯 Base64（不带 data:image/...;base64, 标识头，多张英文逗号分隔）。
/// </summary>
public class RecycleMaterialTransportRecord
{
    [JsonPropertyName("dataNo")]
    public string DataNo { get; set; } = string.Empty;

    [JsonPropertyName("dataStatus")]
    public int? DataStatus { get; set; } = 0;

    [JsonPropertyName("pointNumber")]
    public string PointNumber { get; set; } = string.Empty;

    [JsonPropertyName("carNo")]
    public string CarNo { get; set; } = string.Empty;

    [JsonPropertyName("carrierCompanyName")]
    public string? CarrierCompanyName { get; set; }

    [JsonPropertyName("materialName")]
    public string MaterialName { get; set; } = string.Empty;

    [JsonPropertyName("netWeight")]
    public decimal NetWeight { get; set; }

    [JsonPropertyName("tareWeight")]
    public decimal? TareWeight { get; set; }

    [JsonPropertyName("grossWeight")]
    public decimal? GrossWeight { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal? UnitPrice { get; set; }

    [JsonPropertyName("payAmount")]
    public decimal? PayAmount { get; set; }

    [JsonPropertyName("inTime")]
    public string InTime { get; set; } = string.Empty;

    [JsonPropertyName("inPhoto")]
    public string InPhoto { get; set; } = string.Empty;

    public static RecycleMaterialTransportRecord FromWaybill(
        Waybill waybill,
        string inPhoto,
        string materialName,
        string? carrierCompanyName,
        string? pointNumber)
    {
        return new RecycleMaterialTransportRecord
        {
            DataNo = waybill.OrderNo ?? string.Empty,
            PointNumber = pointNumber ?? string.Empty,
            CarNo = waybill.PlateNumber ?? string.Empty,
            CarrierCompanyName = carrierCompanyName,
            MaterialName = materialName,
            NetWeight = waybill.OrderGoodsWeight ?? 0m,
            TareWeight = waybill.OrderTruckWeight,
            GrossWeight = waybill.OrderTotalWeight,
            InTime = (waybill.JoinTime ?? waybill.AddDate).ToString("yyyy-MM-dd HH:mm:ss"),
            InPhoto = inPhoto
        };
    }
}
