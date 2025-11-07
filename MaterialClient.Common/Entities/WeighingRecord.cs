using Volo.Abp.Domain.Entities.Auditing;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 称重记录实体
/// </summary>
public class WeighingRecord : FullAuditedEntity<long>
{
    /// <summary>
    /// 重量
    /// </summary>
    public decimal Weight { get; set; }

    /// <summary>
    /// 车牌号
    /// </summary>
    public string? PlateNumber { get; set; }

    /// <summary>
    /// 供应商ID (FK to Provider, optional)
    /// </summary>
    public int? ProviderId { get; set; }

    /// <summary>
    /// 物料ID (FK to Material, optional)
    /// </summary>
    public int? MaterialId { get; set; }

    /// <summary>
    /// 记录类型（Unmatch/Join/Out）
    /// </summary>
    public WeighingRecordType RecordType { get; set; } = WeighingRecordType.Unmatch;

    // Navigation properties
    /// <summary>
    /// 供应商导航属性
    /// </summary>
    public Provider? Provider { get; set; }

    /// <summary>
    /// 物料定义导航属性
    /// </summary>
    public Material? Material { get; set; }
}

