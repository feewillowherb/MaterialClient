using System.Text.Json.Serialization;
using MaterialClient.Common.Entities;

namespace MaterialClient.Recycle.Models;

/// <summary>
///     资源化利用厂物料进场运输记录请求 DTO（§2.3 接口 materialTransportRecord/v1/addBatch）。
///     重量字段由 FromWaybill 从吨（Waybill 存储单位）×1000 转换为 kg；时间格式 yyyy-MM-dd HH:mm:ss；
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
        var netWeightTons = waybill.OrderGoodsWeight ?? 0m;
        var tareWeightTons = waybill.OrderTruckWeight;
        var grossWeightTons = waybill.OrderTotalWeight;

        return new RecycleMaterialTransportRecord
        {
            DataNo = waybill.OrderNo ?? string.Empty,
            PointNumber = pointNumber ?? string.Empty,
            CarNo = waybill.PlateNumber ?? string.Empty,
            CarrierCompanyName = carrierCompanyName,
            MaterialName = materialName,
            NetWeight = netWeightTons > 0m ? netWeightTons * 1000m : 0m,
            TareWeight = tareWeightTons.HasValue && tareWeightTons.Value > 0m ? tareWeightTons.Value * 1000m : null,
            GrossWeight = grossWeightTons.HasValue && grossWeightTons.Value > 0m ? grossWeightTons.Value * 1000m : null,
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
