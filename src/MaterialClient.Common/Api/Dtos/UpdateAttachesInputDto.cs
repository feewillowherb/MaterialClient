namespace MaterialClient.Common.Api.Dtos;

public class UpdateAttachesInputDto
{
    /// <summary>
    ///     Desc:其他表主键：例如OrderId, GoodId等
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public string BizId { get; set; }

    /// <summary>
    ///     Desc:业务类型 1.现场照片 2.票据照片
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public int? BizType { get; set; }

    /// <summary>
    ///     Desc:文件名称
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    ///     Desc:文件库名称
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public string Bucket { get; set; }

    /// <summary>
    ///     Desc:OSS文件路径(包括路径和文件名，例如：filePath/Dingtalk_20210618151524.jpg)
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public string BucketKey { get; set; }

    /// <summary>
    ///     Desc:文件大小 KB
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public int? FileSize { get; set; }

    /// <summary>
    ///     Desc:物料状态(0：正常 1：删除)
    ///     Default:0
    ///     Nullable:True
    /// </summary>
    public int? DeleteStatus { get; set; }

    /// <summary>
    ///     Desc:最后编辑人ID
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public int? LastEditUserId { get; set; }

    /// <summary>
    ///     Desc:最后编辑人名称
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public string LastEditor { get; set; }

    /// <summary>
    ///     Desc:
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public int? CreateUserId { get; set; }

    /// <summary>
    ///     Desc:
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public string Creator { get; set; }

    /// <summary>
    ///     Desc:最后更新时间
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public int? UpdateTime { get; set; }

    /// <summary>
    ///     Desc:添加时间
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public int? AddTime { get; set; }

    /// <summary>
    ///     Desc:最后更新时间
    ///     Default:
    ///     Nullable:True
    /// </summary>
    public DateTime? UpdateDate { get; set; }

    /// <summary>
    ///     Desc:添加时间
    ///     Default:DateTime.Now
    ///     Nullable:True
    /// </summary>
    public DateTime? AddDate { get; set; }
}