using System.Text.Json.Serialization;
using MaterialClient.Common.Entities;

namespace MaterialClient.Recycle.Models;

/// <summary>
///     资源化利用厂物料进场运输记录请求 DTO（§2.3 接口 materialTransportRecord/v1/addBatch）。
///     重量单位为「吨」（Waybill 存储单位已是吨，FromWaybill 直接使用）；时间格式 yyyy-MM-dd HH:mm:ss；
///     inPhoto 为纯 Base64（不带 data:image/...;base64, 标识头，多张英文逗号分隔）。
/// </summary>
public record RecycleMaterialTransportRecord
{
    [JsonPropertyName("dataNo")]
    public string DataNo { get; init; } = string.Empty;

    [JsonPropertyName("dataStatus")]
    public int? DataStatus { get; init; } = 0;

    [JsonPropertyName("pointNumber")]
    public string PointNumber { get; init; } = string.Empty;

    [JsonPropertyName("carNo")]
    public string CarNo { get; init; } = string.Empty;

    [JsonPropertyName("carrierCompanyName")]
    public string? CarrierCompanyName { get; init; }

    [JsonPropertyName("materialName")]
    public string MaterialName { get; init; } = string.Empty;

    [JsonPropertyName("netWeight")]
    public decimal NetWeight { get; init; }

    [JsonPropertyName("tareWeight")]
    public decimal? TareWeight { get; init; }

    [JsonPropertyName("grossWeight")]
    public decimal? GrossWeight { get; init; }

    [JsonPropertyName("unitPrice")]
    public decimal? UnitPrice { get; init; }

    [JsonPropertyName("payAmount")]
    public decimal? PayAmount { get; init; }

    [JsonPropertyName("inTime")]
    public string InTime { get; init; } = string.Empty;

    [JsonPropertyName("inPhoto")]
    public string InPhoto { get; init; } = string.Empty;

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

    /// <summary>
    ///     Returns a copy safe for structured logging (photo base64 content omitted).
    /// </summary>
    public RecycleMaterialTransportRecord ForLogging() =>
        this with
        {
            InPhoto = string.IsNullOrEmpty(InPhoto)
                ? InPhoto
                : $"[{InPhoto.Length} chars omitted]"
        };
}
