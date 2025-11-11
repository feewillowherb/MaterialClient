using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 运单-附件关联实体
/// </summary>
public class WaybillAttachment : Entity<int>
{
    /// <summary>
    /// 构造函数（用于EF Core）
    /// </summary>
    protected WaybillAttachment()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public WaybillAttachment(int id, long waybillId, int attachmentFileId)
        : base(id)
    {
        WaybillId = waybillId;
        AttachmentFileId = attachmentFileId;
    }

    /// <summary>
    /// 运单ID (FK to Waybill)
    /// </summary>
    public long WaybillId { get; set; }

    /// <summary>
    /// 附件文件ID (FK to AttachmentFile)
    /// </summary>
    public int AttachmentFileId { get; set; }

    // Navigation properties
    /// <summary>
    /// 运单导航属性
    /// </summary>
    public Waybill? Waybill { get; set; }

    /// <summary>
    /// 附件文件导航属性
    /// </summary>
    public AttachmentFile? AttachmentFile { get; set; }
}

