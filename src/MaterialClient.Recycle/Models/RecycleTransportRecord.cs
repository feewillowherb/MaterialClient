using System.Text.Json.Serialization;

namespace MaterialClient.Recycle.Models;

/// <summary>
///     资源化利用厂出场运输记录请求 DTO（§2.2 接口 productTransportRecord/v1/addBatch）。
///     重量单位为「吨」（由 kg ÷ 1000 转换）；时间格式 yyyy-MM-dd HH:mm:ss；
///     outPhotos 为纯 Base64（不带 data:image/...;base64, 标识头，多张英文逗号分隔）。
///     JSON 字段名按 §2.2 文档要求为 camelCase，使用 <see cref="JsonPropertyNameAttribute"/> 显式声明，
///     避免依赖 Refit 默认序列化策略。
/// </summary>
public class RecycleTransportRecord
{
    /// <summary>数据唯一标识（必填）</summary>
    [JsonPropertyName("dataNo")]
    public string DataNo { get; set; } = string.Empty;

    /// <summary>数据状态（可选，默认 0）</summary>
    [JsonPropertyName("dataStatus")]
    public int? DataStatus { get; set; } = 0;

    /// <summary>资源化利用厂唯一标识（必填）</summary>
    [JsonPropertyName("pointNumber")]
    public string PointNumber { get; set; } = string.Empty;

    /// <summary>车牌号（必填）</summary>
    [JsonPropertyName("carNo")]
    public string CarNo { get; set; } = string.Empty;

    /// <summary>运输单位/公司名称（可选）</summary>
    [JsonPropertyName("carrierCompanyName")]
    public string? CarrierCompanyName { get; set; }

    /// <summary>成品名称（必填）</summary>
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>净重（吨，必填）</summary>
    [JsonPropertyName("netWeight")]
    public decimal NetWeight { get; set; }

    /// <summary>皮重（吨，可选）</summary>
    [JsonPropertyName("tareWeight")]
    public decimal? TareWeight { get; set; }

    /// <summary>毛重（吨，可选）</summary>
    [JsonPropertyName("grossWeight")]
    public decimal? GrossWeight { get; set; }

    /// <summary>单价（元/吨，可选）</summary>
    [JsonPropertyName("unitPrice")]
    public decimal? UnitPrice { get; set; }

    /// <summary>结算金额（元，可选）</summary>
    [JsonPropertyName("payAmount")]
    public decimal? PayAmount { get; set; }

    /// <summary>出场时间（必填，格式 yyyy-MM-dd HH:mm:ss）</summary>
    [JsonPropertyName("outTime")]
    public string OutTime { get; set; } = string.Empty;

    /// <summary>出场照片 Base64（必填，不带标识头，多张英文逗号分隔）</summary>
    [JsonPropertyName("outPhotos")]
    public string OutPhotos { get; set; } = string.Empty;

    /// <summary>销售合同编号（可选）</summary>
    [JsonPropertyName("saleContractNo")]
    public string? SaleContractNo { get; set; }

    /// <summary>收货方（可选）</summary>
    [JsonPropertyName("consignee")]
    public string? Consignee { get; set; }

    /// <summary>收货地址（可选）</summary>
    [JsonPropertyName("consigneeAddress")]
    public string? ConsigneeAddress { get; set; }

    /// <summary>收货时间（可选，格式 yyyy-MM-dd HH:mm:ss）</summary>
    [JsonPropertyName("receivingTime")]
    public string? ReceivingTime { get; set; }

    /// <summary>收货照片 Base64（可选）</summary>
    [JsonPropertyName("receivingProof")]
    public string? ReceivingProof { get; set; }
}
