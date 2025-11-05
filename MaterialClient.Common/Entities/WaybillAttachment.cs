using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 运单-附件关联实体
/// </summary>
public class WaybillAttachment : Entity<int>
{
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

