using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 称重记录-附件关联实体
/// </summary>
public class WeighingRecordAttachment : Entity<int>
{
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

