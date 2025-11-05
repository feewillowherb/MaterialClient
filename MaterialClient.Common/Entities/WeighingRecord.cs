using Volo.Abp.Domain.Entities.Auditing;

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
    /// 物料ID (FK to MaterialDefinition, optional)
    /// </summary>
    public int? MaterialId { get; set; }

    // Navigation properties
    /// <summary>
    /// 供应商导航属性
    /// </summary>
    public Provider? Provider { get; set; }

    /// <summary>
    /// 物料定义导航属性
    /// </summary>
    public MaterialDefinition? Material { get; set; }
}

