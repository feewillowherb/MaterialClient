namespace MaterialClient.Common.Entities;

public interface IMaterialClientAuditedObject
{
    /// <summary>
    ///     最后编辑人ID
    /// </summary>
    public int? LastEditUserId { get; set; }

    /// <summary>
    ///     最后编辑人名称
    /// </summary>
    public string? LastEditor { get; set; }

    /// <summary>
    ///     创建人ID
    /// </summary>
    public int? CreateUserId { get; set; }

    /// <summary>
    ///     创建人名称
    /// </summary>
    public string? Creator { get; set; }

    /// <summary>
    ///     最后更新时间（时间戳）
    /// </summary>
    public int? UpdateTime { get; set; }

    /// <summary>
    ///     添加时间（时间戳）
    /// </summary>
    public int? AddTime { get; set; }

    /// <summary>
    ///     最后更新时间
    /// </summary>
    public DateTime? UpdateDate { get; set; }

    /// <summary>
    ///     添加时间
    /// </summary>
    public DateTime? AddDate { get; set; }
}