using Volo.Abp.Domain.Entities.Auditing;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 附件文件实体
/// </summary>
public class AttachmentFile : FullAuditedEntity<int>
{
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

