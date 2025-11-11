using Volo.Abp.Domain.Entities.Auditing;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 附件文件实体
/// </summary>
public class AttachmentFile : FullAuditedEntity<int>
{
    /// <summary>
    /// 构造函数（用于EF Core）
    /// </summary>
    protected AttachmentFile()
    {
    }

    /// <summary>
    /// 构造函数（用于自增主键）
    /// </summary>
    public AttachmentFile(string fileName, string localPath, AttachType attachType)
    {
        FileName = fileName;
        LocalPath = localPath;
        AttachType = attachType;
    }

    /// <summary>
    /// 构造函数（用于指定Id）
    /// </summary>
    public AttachmentFile(int id, string fileName, string localPath, AttachType attachType)
        : base(id)
    {
        FileName = fileName;
        LocalPath = localPath;
        AttachType = attachType;
    }

    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 本地路径
    /// </summary>
    public string LocalPath { get; set; } = string.Empty;

    /// <summary>
    /// OSS完整路径
    /// </summary>
    public string? OssFullPath { get; set; }

    /// <summary>
    /// 附件类型
    /// </summary>
    public AttachType AttachType { get; set; }
}

