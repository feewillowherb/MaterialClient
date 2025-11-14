using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 称重记录-附件关联实体
/// </summary>
public class WeighingRecordAttachment : Entity<int>
{
    /// <summary>
    /// 构造函数（用于EF Core）
    /// </summary>
    protected WeighingRecordAttachment()
    {
    }

    /// <summary>
    /// 构造函数（用于自增主键）
    /// </summary>
    public WeighingRecordAttachment(long weighingRecordId, int attachmentFileId)
    {
        WeighingRecordId = weighingRecordId;
        AttachmentFileId = attachmentFileId;
    }

    /// <summary>
    /// 构造函数（用于指定Id）
    /// </summary>
    public WeighingRecordAttachment(int id, long weighingRecordId, int attachmentFileId)
        : base(id)
    {
        WeighingRecordId = weighingRecordId;
        AttachmentFileId = attachmentFileId;
    }

    /// <summary>
    /// 称重记录ID (FK to WeighingRecord)
    /// </summary>
    public long WeighingRecordId { get; set; }

    /// <summary>
    /// 附件文件ID (FK to AttachmentFile)
    /// </summary>
    public int AttachmentFileId { get; set; }

    // Navigation properties
    /// <summary>
    /// 称重记录导航属性
    /// </summary>
    public WeighingRecord? WeighingRecord { get; set; }

    /// <summary>
    /// 附件文件导航属性
    /// </summary>
    public AttachmentFile? AttachmentFile { get; set; }
}

