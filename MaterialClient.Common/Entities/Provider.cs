using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
///     供应商实体
/// </summary>
public class Provider : Entity<int>, IMaterialClientAuditedObject, IDeletionAuditedObject
{
    /// <summary>
    ///     构造函数（用于EF Core）
    /// </summary>
    protected Provider()
    {
    }

    /// <summary>
    ///     构造函数（用于自增主键）
    /// </summary>
    public Provider(int providerType, string providerName)
    {
        ProviderType = providerType;
        ProviderName = providerName;
    }

    /// <summary>
    ///     构造函数（用于指定Id）
    /// </summary>
    public Provider(int id, int providerType, string providerName)
        : base(id)
    {
        ProviderType = providerType;
        ProviderName = providerName;
    }

    /// <summary>
    ///     供应商类型
    /// </summary>
    public int? ProviderType { get; set; }

    /// <summary>
    ///     供应商名称
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;


    /// <summary>
    ///     Desc:供应商类型名称
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public string? ProviderTypeName { get; set; }

    /// <summary>
    ///     联系人姓名
    /// </summary>
    public string? ContectName { get; set; }

    /// <summary>
    ///     联系人电话
    /// </summary>
    public string? ContectPhone { get; set; }


    public int? MaterialTypeId { get; set; }


    public int? CoId { get; set; }


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