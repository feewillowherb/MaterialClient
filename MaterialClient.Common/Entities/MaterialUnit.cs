using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 物料单位实体
/// </summary>
public class MaterialUnit : Entity<int>
{
    /// <summary>
    /// 物料ID (FK to Material)
    /// </summary>
    public int MaterialId { get; set; }

    /// <summary>
    /// 单位名称
    /// </summary>
    public string UnitName { get; set; } = string.Empty;

    /// <summary>
    /// 换算率
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// 供应商ID (FK to Provider, optional)
    /// </summary>
    public int? ProviderId { get; set; }

    /// <summary>
    /// 换算率名称
    /// </summary>
    public string? RateName { get; set; }

    // Navigation properties
    /// <summary>
    /// 物料定义导航属性
    /// </summary>
    public Material? Material { get; set; }

    /// <summary>
    /// 供应商导航属性
    /// </summary>
    public Provider? Provider { get; set; }
}

