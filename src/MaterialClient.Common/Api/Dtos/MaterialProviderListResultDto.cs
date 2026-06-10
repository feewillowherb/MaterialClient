using MaterialClient.Common.Entities;

namespace MaterialClient.Common.Api.Dtos;

public class MaterialProviderListResultDto
{
    /// <summary>
    ///     Desc:供应商编号
    ///     Default:
    ///     Nullable:False
    /// </summary>
    public int ProviderId { get; set; }

    /// <summary>
    ///     Desc:供应商类型编号
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public int? ProviderType { get; set; }

    /// <summary>
    ///     Desc:供应商类型名称
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public string? ProviderTypeName { get; set; }

    /// <summary>
    ///     Desc:供应商名称
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    ///     Desc:联系人
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public string? ContectName { get; set; }

    /// <summary>
    ///     Desc:联系方式
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public string? ContectPhone { get; set; }

    /// <summary>
    ///     Desc:最近更新人编号
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public int? LastEditUserId { get; set; }

    /// <summary>
    ///     Desc:最近更新人
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public string? LastEditor { get; set; }

    /// <summary>
    ///     Desc:(0:正常 1:删除)
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public int? DeleteStatus { get; set; }

    /// <summary>
    ///     Desc:创建人编号
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public int? CreateUserId { get; set; }

    /// <summary>
    ///     Desc:创建人
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public string? Creator { get; set; }

    /// <summary>
    ///     Desc:更新时间
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public DateTime? UpdateDate { get; set; }

    /// <summary>
    ///     Desc:更新时间戳
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public int? UpdateTime { get; set; }

    /// <summary>
    ///     Desc:添加时间
    ///     Default:DateTime.Now
    ///     Nullable:True
    /// </summary>
    public DateTime? AddDate { get; set; }

    /// <summary>
    ///     Desc:添加时间（时间戳）
    ///     Default:DateTime.Now
    ///     Nullable:True
    /// </summary>
    public int? AddTime { get; set; }

    /// <summary>
    ///     公司id
    /// </summary>
    public int? CoId { get; set; } = 0;

    public int? MaterialTypeId { get; set; }

    /// <summary>
    ///     转换为实体对象
    /// </summary>
    public static Provider ToEntity(MaterialProviderListResultDto dto)
    {
        var provider = new Provider(
            dto.ProviderId,
            dto.ProviderType ?? 0,
            dto.ProviderName ?? string.Empty)
        {
            ProviderTypeName = dto.ProviderTypeName,
            ContectName = dto.ContectName,
            ContectPhone = dto.ContectPhone,
            MaterialTypeId = dto.MaterialTypeId,
            CoId = dto.CoId,
            IsDeleted = dto.DeleteStatus == 1,
            LastEditUserId = dto.LastEditUserId,
            LastEditor = dto.LastEditor,
            CreateUserId = dto.CreateUserId,
            Creator = dto.Creator,
            UpdateTime = dto.UpdateTime,
            AddTime = dto.AddTime ?? 0,
            UpdateDate = dto.UpdateDate,
            AddDate = dto.AddDate ?? DateTime.Now
        };

        return provider;
    }
}