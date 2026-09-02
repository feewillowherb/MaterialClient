using System.Text.Json.Serialization;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Urban.Dtos;

/// <summary>
///     称重记录提交 DTO（发送到 UrbanManagement 服务端）
///     字段与 UrbanManagement.App/Models/UrbanWeighingRecordDto 对齐
/// </summary>
public class UrbanWeighingRecordSubmitDto
{
    /// <summary>客户端扩展 ID（<c>UrbanWeighingExtension.Id</c>，用于幂等去重）。</summary>
    [JsonPropertyName("clientRecordId")] public Guid ClientRecordId { get; set; }

    [JsonPropertyName("plateNumber")] public string? PlateNumber { get; set; }

    /// <summary>总重量，单位：千克（kg）；由本地吨值经 <c>MaterialMath.ConvertTonToKg</c> 换算。</summary>
    [JsonPropertyName("totalWeight")]
    public decimal TotalWeight { get; set; }

    [JsonPropertyName("weighingTime")] public DateTime WeighingTime { get; set; }

    [JsonPropertyName("syncType")] public int? SyncType { get; set; }

    [JsonPropertyName("vehicleColor")] public string? VehicleColor { get; set; }

    [JsonPropertyName("plateColor")] public string? PlateColor { get; set; }

    [JsonPropertyName("vehicleType")] public string? VehicleType { get; set; }

    [JsonPropertyName("deviceId")] public string? DeviceId { get; set; }

    [JsonPropertyName("buildLicenseNo")] public string? BuildLicenseNo { get; set; }

    [JsonPropertyName("siteType")] public UrbanSiteType SiteType { get; set; } = UrbanSiteType.Construction;

    [JsonPropertyName("proId")] public Guid ProId { get; set; }

    [JsonPropertyName("proName")] public string? ProName { get; set; }

    /// <summary>提交该称重数据的客户端机器码（F2 数据溯源，仅记录不校验）。</summary>
    [JsonPropertyName("submitMachineCode")] public string? SubmitMachineCode { get; set; }

    [JsonPropertyName("isAnomaly")] public bool IsAnomaly { get; set; }

    [JsonPropertyName("anomalyReason")] public string? AnomalyReason { get; set; }

    /// <summary>扩展属性字典，用于传递编辑历史等扩展数据。</summary>
    [JsonPropertyName("extraProperties")]
    public Dictionary<string, object?>? ExtraProperties { get; set; }

    [JsonPropertyName("clientSyncType")] public int? ClientSyncType { get; set; }

    [JsonPropertyName("clientSyncTime")] public DateTime? ClientSyncTime { get; set; }

    [JsonPropertyName("clientRetryCount")] public int? ClientRetryCount { get; set; }

    [JsonPropertyName("clientLastErrorTime")] public DateTime? ClientLastErrorTime { get; set; }

    [JsonPropertyName("attachmentIds")] public List<Guid>? AttachmentIds { get; set; }
}
