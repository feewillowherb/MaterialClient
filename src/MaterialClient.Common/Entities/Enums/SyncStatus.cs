using System.ComponentModel;

namespace MaterialClient.Common.Entities.Enums;

/// <summary>
///     称重记录同步状态
/// </summary>
public enum SyncStatus
{
    /// <summary>
    ///     待上传
    /// </summary>
    [Description("待上传")] Pending = 0,

    /// <summary>
    ///     已同步
    /// </summary>
    [Description("已同步")] Synced = 1,

    /// <summary>
    ///     上传失败
    /// </summary>
    [Description("上传失败")] Failed = 2
}
