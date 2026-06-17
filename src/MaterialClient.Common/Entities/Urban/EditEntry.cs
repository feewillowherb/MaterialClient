using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Entities.Urban;

/// <summary>
///     称重记录单次修改条目，存储于
///     <see cref="UrbanWeighingExtension" /> <c>ExtraProperties["EditHistory"]</c>。
/// </summary>
public class EditEntry
{
    /// <summary>
    ///     修改发生时间（本地时间）
    /// </summary>
    public DateTime ChangedAt { get; set; }

    /// <summary>
    ///     修改前的字段快照
    /// </summary>
    public EditEntrySnapshot Before { get; set; } = new();

    /// <summary>
    ///     修改后的字段快照
    /// </summary>
    public EditEntrySnapshot After { get; set; } = new();

    /// <summary>
    ///     修改来源（客户端 / 服务端）
    /// </summary>
    public EditSource Source { get; set; }

    /// <summary>
    ///     是否修改过图片（预留字段，当前功能未实现）
    /// </summary>
    public bool IsImagesModified { get; set; }
}
