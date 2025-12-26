using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
///     物料定义实体
/// </summary>
public class Material : Entity<int>, IMaterialClientAuditedObject, IDeletionAuditedObject
{
    /// <summary>
    ///     构造函数（用于EF Core）
    /// </summary>
    protected Material()
    {
    }

    /// <summary>
    ///     构造函数（用于自增主键）
    /// </summary>
    public Material(string name, int coId)
    {
        Name = name;
        CoId = coId;
    }

    /// <summary>
    ///     构造函数（用于指定Id）
    /// </summary>
    public Material(int id, string name, int coId)
        : base(id)
    {
        Name = name;
        CoId = coId;
    }

    /// <summary>
    ///     物料名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     品牌
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    ///     规格尺寸
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    ///     上限
    /// </summary>
    public decimal? UpperLimit { get; set; }

    /// <summary>
    ///     下限
    /// </summary>
    public decimal? LowerLimit { get; set; }

    /// <summary>
    ///     基本单位
    /// </summary>
    public string? BasicUnit { get; set; }

    /// <summary>
    ///     物料编码
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    ///     公司ID
    /// </summary>
    public int CoId { get; set; }

    /// <summary>
    ///     规格说明
    /// </summary>
    public string? Specifications { get; set; }

    /// <summary>
    ///     产品ID
    /// </summary>
    public string? ProId { get; set; }

    /// <summary>
    ///     单位名称
    /// </summary>
    public string? UnitName { get; set; }

    /// <summary>
    ///     单位换算率
    /// </summary>
    public decimal UnitRate { get; set; } = 1;

    #region Audited Properties

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

    #endregion
}