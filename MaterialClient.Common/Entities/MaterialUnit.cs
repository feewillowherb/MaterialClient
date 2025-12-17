using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 物料单位实体
/// </summary>
public class MaterialUnit : Entity<int>, IDeletionAuditedObject, IMaterialClientAuditedObject
{
    /// <summary>
    /// 构造函数（用于EF Core）
    /// </summary>
    protected MaterialUnit()
    {
    }

    /// <summary>
    /// 构造函数（用于自增主键）
    /// </summary>
    public MaterialUnit(int materialId, string unitName, decimal rate)
    {
        MaterialId = materialId;
        UnitName = unitName;
        Rate = rate;
    }

    /// <summary>
    /// 构造函数（用于指定Id）
    /// </summary>
    public MaterialUnit(int id, int materialId, string unitName, decimal rate)
        : base(id)
    {
        MaterialId = materialId;
        UnitName = unitName;
        Rate = rate;
    }

    /// <summary>
    /// 物料ID (FK to Material)
    /// </summary>
    public int MaterialId { get; set; }


    /// <summary>
    /// Desc:单位计算类型(0:按重量  1:按数量)
    /// Default:0
    /// Nullable:True
    /// </summary>           
    public int? UnitCalculationType { get; set; }

    /// <summary>
    /// 单位名称
    /// </summary>
    public string UnitName { get; set; } = string.Empty;

    /// <summary>
    /// 换算率
    /// </summary>
    public decimal? Rate { get; set; }

    /// <summary>
    /// 供应商ID (FK to Provider, optional)
    /// </summary>
    public int? ProviderId { get; set; }

    /// <summary>
    /// 换算率名称
    /// </summary>
    public string? RateName { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletionTime { get; set; }
    public Guid? DeleterId { get; set; }
    public int? LastEditUserId { get; set; }
    public string? LastEditor { get; set; }
    public int? CreateUserId { get; set; }
    public string? Creator { get; set; }
    public int? UpdateTime { get; set; }
    public int? AddTime { get; set; }
    public DateTime? UpdateDate { get; set; }
    public DateTime? AddDate { get; set; }
}