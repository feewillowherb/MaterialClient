using MaterialClient.Common.Entities.Enums;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;

namespace MaterialClient.Common.Entities;

/// <summary>
///     附件文件实体
/// </summary>
public class AttachmentFile : Entity<int>, IMaterialClientAuditedObject, IDeletionAuditedObject
{
    /// <summary>
    ///     构造函数（用于EF Core）
    /// </summary>
    protected AttachmentFile()
    {
    }

    /// <summary>
    ///     构造函数（用于自增主键）
    /// </summary>
    public AttachmentFile(string fileName, string localPath, AttachType attachType)
    {
        FileName = fileName;
        LocalPath = localPath;
        AttachType = attachType;
    }

    /// <summary>
    ///     构造函数（用于指定Id）
    /// </summary>
    public AttachmentFile(int id, string fileName, string localPath, AttachType attachType)
        : base(id)
    {
        FileName = fileName;
        LocalPath = localPath;
        AttachType = attachType;
    }

    /// <summary>
    ///     文件名
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    ///     本地路径
    /// </summary>
    public string LocalPath { get; set; } = string.Empty;

    /// <summary>
    ///     OSS完整路径
    /// </summary>
    public string? OssFullPath { get; set; }

    /// <summary>
    ///     附件类型
    /// </summary>
    public AttachType AttachType { get; set; }

    /// <summary>
    ///     最后同步到OSS的时间
    /// </summary>
    public DateTime? LastSyncTime { get; set; }

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