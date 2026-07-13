using System.Text.Json.Serialization;
using MaterialClient.Common.Entities;

namespace MaterialClient.Recycle.Models;

/// <summary>
///     资源化利用厂出场运输记录请求 DTO（§2.2 接口 productTransportRecord/v1/addBatch）。
///     重量单位为「吨」（Waybill 存储单位已是吨，FromWaybill 直接使用）；时间格式 yyyy-MM-dd HH:mm:ss；
///     outPhotos 为纯 Base64（不带 data:image/...;base64, 标识头，多张英文逗号分隔）。
///     JSON 字段名按 §2.2 文档要求为 camelCase，使用 <see cref="JsonPropertyNameAttribute"/> 显式声明，
///     避免依赖 Refit 默认序列化策略。
/// </summary>
public record RecycleTransportRecord
{
    /// <summary>数据唯一标识（必填）</summary>
    [JsonPropertyName("dataNo")]
    public string DataNo { get; init; } = string.Empty;

    /// <summary>数据状态（可选，默认 0）</summary>
    [JsonPropertyName("dataStatus")]
    public int? DataStatus { get; init; } = 0;

    /// <summary>资源化利用厂唯一标识（必填）</summary>
    [JsonPropertyName("pointNumber")]
    public string PointNumber { get; init; } = string.Empty;

    /// <summary>车牌号（必填）</summary>
    [JsonPropertyName("carNo")]
    public string CarNo { get; init; } = string.Empty;

    /// <summary>运输单位/公司名称（可选）</summary>
    [JsonPropertyName("carrierCompanyName")]
    public string? CarrierCompanyName { get; init; }

    /// <summary>成品名称（必填，§2.2 字段 productName，映射自物料 <c>Material.Name</c>）。</summary>
    [JsonPropertyName("productName")]
    public string ProductName { get; init; } = string.Empty;

    /// <summary>净重（吨，必填）</summary>
    [JsonPropertyName("netWeight")]
    public decimal NetWeight { get; init; }

    /// <summary>皮重（吨，可选）</summary>
    [JsonPropertyName("tareWeight")]
    public decimal? TareWeight { get; init; }

    /// <summary>毛重（吨，可选）</summary>
    [JsonPropertyName("grossWeight")]
    public decimal? GrossWeight { get; init; }

    /// <summary>单价（元/吨，可选）</summary>
    [JsonPropertyName("unitPrice")]
    public decimal? UnitPrice { get; init; }

    /// <summary>结算金额（元，可选）</summary>
    [JsonPropertyName("payAmount")]
    public decimal? PayAmount { get; init; }

    /// <summary>出场时间（必填，格式 yyyy-MM-dd HH:mm:ss）</summary>
    [JsonPropertyName("outTime")]
    public string OutTime { get; init; } = string.Empty;

    /// <summary>出场照片 Base64（必填，不带标识头，多张英文逗号分隔）</summary>
    [JsonPropertyName("outPhotos")]
    public string OutPhotos { get; init; } = string.Empty;

    /// <summary>销售合同编号（可选）</summary>
    [JsonPropertyName("saleContractNo")]
    public string? SaleContractNo { get; init; }

    /// <summary>收货方（可选）</summary>
    [JsonPropertyName("consignee")]
    public string? Consignee { get; init; }

    /// <summary>收货地址（可选）</summary>
    [JsonPropertyName("consigneeAddress")]
    public string? ConsigneeAddress { get; init; }

    /// <summary>收货时间（可选，格式 yyyy-MM-dd HH:mm:ss）</summary>
    [JsonPropertyName("receivingTime")]
    public string? ReceivingTime { get; init; }

    /// <summary>收货照片 Base64（可选）</summary>
    [JsonPropertyName("receivingProof")]
    public string? ReceivingProof { get; init; }

    /// <summary>
    ///     从 <see cref="Waybill" /> 创建 §2.2 运输记录。
    ///     字段来源：Waybill.OrderNo 作为 dataNo（OrderNo 为空时由调用方跳过上报，本方法不做 R-{id} 回退）；
    ///     重量单位已是吨，直接使用；consignee 来自 Provider.ProviderName。
    /// </summary>
    public static RecycleTransportRecord FromWaybill(
        Waybill waybill,
        string outPhotos,
        string productName,
        string? pointNumber,
        string? consignee = null)
    {
        var netWeightTons = waybill.OrderGoodsWeight ?? 0m;
        var tareWeightTons = waybill.OrderTruckWeight;
        var grossWeightTons = waybill.OrderTotalWeight;
        var outTime = waybill.OutTime ?? waybill.AddDate;
        var dataNo = waybill.OrderNo ?? string.Empty;
        var carNo = waybill.PlateNumber ?? string.Empty;

        return new RecycleTransportRecord
        {
            DataNo = dataNo,
            PointNumber = pointNumber ?? string.Empty,
            CarNo = carNo,
            Consignee = consignee,
            ProductName = productName,
            NetWeight = netWeightTons,
            TareWeight = tareWeightTons.HasValue && tareWeightTons.Value > 0 ? tareWeightTons.Value : null,
            GrossWeight = grossWeightTons.HasValue && grossWeightTons.Value > 0 ? grossWeightTons.Value : null,
            OutTime = outTime.ToString("yyyy-MM-dd HH:mm:ss"),
            OutPhotos = outPhotos
        };
    }

    /// <summary>
    ///     Returns a copy safe for structured logging (photo base64 content omitted).
    /// </summary>
    public RecycleTransportRecord ForLogging() =>
        this with
        {
            OutPhotos = string.IsNullOrEmpty(OutPhotos)
                ? OutPhotos
                : $"[{OutPhotos.Length} chars omitted]",
            ReceivingProof = string.IsNullOrEmpty(ReceivingProof)
                ? ReceivingProof
                : $"[{ReceivingProof.Length} chars omitted]"
        };
}
