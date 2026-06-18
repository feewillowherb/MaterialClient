using System.Text.Json.Serialization;

namespace MaterialClient.Urban.Dtos;

/// <summary>
///     审批称重记录 DTO（发送到 UrbanManagement 服务端）
///     字段与 UrbanManagement.Core.Models.UrbanWeighingRecordApproveInputDto 对齐
/// </summary>
public class UrbanWeighingRecordApproveDto
{
    /// <summary>服务端记录 ID（Web 端审批使用）。客户端审批时为空。</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>客户端记录 ID（MaterialClient 审批使用）。</summary>
    [JsonPropertyName("clientRecordId")]
    public long? ClientRecordId { get; set; }

    [JsonPropertyName("plateNumber")] public string PlateNumber { get; set; } = string.Empty;

    [JsonPropertyName("totalWeight")] public decimal TotalWeight { get; set; }

    /// <summary>可选：Lrp 车牌识别图片替换（Base64 编码）。</summary>
    [JsonPropertyName("lrpReplacementBase64")]
    public string? LrpReplacementBase64 { get; set; }

    /// <summary>可选：UrbanPhoto 城市拍照替换（Base64 编码）。</summary>
    [JsonPropertyName("urbanPhotoReplacementBase64")]
    public string? UrbanPhotoReplacementBase64 { get; set; }
}
