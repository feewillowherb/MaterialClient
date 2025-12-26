using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

public class MaterialType : Entity<int>, IMaterialClientAuditedObject, IDeletionAuditedObject
{
    protected MaterialType()
    {
    }

    /// <summary>
    ///     构造函数（用于指定Id）
    /// </summary>
    public MaterialType(int id, string typeName)
        : base(id)
    {
        TypeName = typeName;
    }

    /// <summary>
    ///     物料类型名称
    /// </summary>
    public string? TypeName { get; set; }

    /// <summary>
    ///     备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    ///     父级ID
    /// </summary>
    public int ParentId { get; set; }

    /// <summary>
    ///     类型代码
    /// </summary>
    public string? TypeCode { get; set; }

    /// <summary>
    ///     公司ID
    /// </summary>
    public int CoId { get; set; }

    /// <summary>
    ///     上限
    /// </summary>
    public decimal UpperLimit { get; set; }

    /// <summary>
    ///     下限
    /// </summary>
    public decimal LowerLimit { get; set; }

    /// <summary>
    ///     项目ID
    /// </summary>
    public Guid? ProId { get; set; }

    #region Audited Properties

    public int? LastEditUserId { get; set; }
    public string? LastEditor { get; set; }
    public int? CreateUserId { get; set; }
    public string? Creator { get; set; }
    public int? UpdateTime { get; set; }
    public int? AddTime { get; set; }
    public DateTime? UpdateDate { get; set; }
    public DateTime? AddDate { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletionTime { get; set; }
    public Guid? DeleterId { get; set; }

    #endregion
}