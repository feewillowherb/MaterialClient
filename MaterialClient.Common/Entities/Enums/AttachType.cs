using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

/// <summary>
/// 附件类型
/// </summary>
public enum AttachType : short
{
    /// <summary>
    /// 进场照片
    /// </summary>
    [Description("进场照片")]
    EntryPhoto = 0,

    /// <summary>
    /// 出场照片
    /// </summary>
    [Description("出场照片")]
    ExitPhoto = 1,

    /// <summary>
    /// 票据照片
    /// </summary>
    [Description("票据照片")]
    TicketPhoto = 2
}

