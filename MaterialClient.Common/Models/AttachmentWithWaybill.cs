using MaterialClient.Common.Entities;

namespace MaterialClient.Common.Models;

/// <summary>
/// 附件与运单关联信息
/// </summary>
public record AttachmentWithWaybill(AttachmentFile Attachment, long WaybillId);

