using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using MaterialClient.Common.Entities.Enums;

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

    public void UpdateInfo(string providerName, string? contactName, string? contactPhone)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name is required.", nameof(providerName));
        }

        ProviderName = providerName.Trim();
        ContectName = string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim();
        ContectPhone = string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone.Trim();
    }


    public int? MaterialTypeId { get; set; }


    public int? CoId { get; set; }

    /// <summary>
    ///     收货地址（本地专用字段）。
    ///     作为 §2.2 productTransportRecord/v1/addBatch 接口 <c>consigneeAddress</c> 的本地数据源。
    ///     本字段不进入远端 <c>CreateProviderInput</c>/<c>UpdateProviderInput</c>/<c>MaterialProviderListResultDto</c> 契约，
    ///     数据库列可空；为本地专用、可选可空字段，内联新建供应商表单可选录入，缺失时落库为 null。
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    ///     称重模式（用于隔离不同模式的数据）
    /// </summary>
    public WeighingMode WeighingMode { get; set; } = WeighingMode.Standard;


    #region Audited Properties

    public int? LastEditUserId { get; set; }
    public string? LastEditor { get; set; }
    public int? CreateUserId { get; set; }
    public string? Creator { get; set; }
    public int? UpdateTime { get; set; }
    public int AddTime { get; set; }
    public DateTime? UpdateDate { get; set; }
    public DateTime AddDate { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletionTime { get; set; }
    public Guid? DeleterId { get; set; }

    #endregion
}