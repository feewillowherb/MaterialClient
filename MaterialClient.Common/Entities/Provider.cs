using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 供应商实体
/// </summary>
public class Provider : Entity<int>
{
    /// <summary>
    /// 构造函数（用于EF Core）
    /// </summary>
    protected Provider()
    {
    }

    /// <summary>
    /// 构造函数（用于自增主键）
    /// </summary>
    public Provider(int providerType, string providerName)
    {
        ProviderType = providerType;
        ProviderName = providerName;
    }

    /// <summary>
    /// 构造函数（用于指定Id）
    /// </summary>
    public Provider(int id, int providerType, string providerName)
        : base(id)
    {
        ProviderType = providerType;
        ProviderName = providerName;
    }

    /// <summary>
    /// 供应商类型
    /// </summary>
    public int ProviderType { get; set; }

    /// <summary>
    /// 供应商名称
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// 联系人姓名
    /// </summary>
    public string? ContactName { get; set; }

    /// <summary>
    /// 联系人电话
    /// </summary>
    public string? ContactPhone { get; set; }
}

