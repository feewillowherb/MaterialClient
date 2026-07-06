using MaterialClient.Common.Dtos.Urban;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.Urban;

/// <summary>
///     Urban 称重扩展领域服务：管理 <see cref="UrbanWeighingExtension" /> 与 <see cref="WeighingRecord" /> 的逻辑关联。
/// </summary>
public interface IUrbanWeighingExtensionService : ITransientDependency
{
    /// <summary>
    ///     在父称重记录已持久化后创建扩展行。
    /// </summary>
    Task<UrbanWeighingExtension> CreateForRecordAsync(long weighingRecordId, bool hasLprAttachment = true);

    /// <summary>
    ///     按扩展 Id 查询。
    /// </summary>
    Task<UrbanWeighingExtension?> GetByIdAsync(Guid extensionId);

    /// <summary>
    ///     按称重记录 Id 查询扩展。
    /// </summary>
    Task<UrbanWeighingExtension?> GetByWeighingRecordIdAsync(long weighingRecordId);

    /// <summary>
    ///     分页查询 Urban 称重列表项（LEFT JOIN 投影为 DTO，不返回实体）。
    /// </summary>
    Task<PagedResultDto<UrbanWeighingListItemDto>> GetPagedListItemsAsync(GetUrbanWeighingListInput input);

    /// <summary>
    ///     查询待上传的扩展行（后台同步 worker 使用）。
    /// </summary>
    Task<List<UrbanWeighingExtension>> GetPendingForUploadAsync(int maxCount = 100);

    /// <summary>
    ///     更新扩展同步状态。
    /// </summary>
    Task UpdateSyncStatusAsync(Guid extensionId, SyncStatus syncStatus, DateTime? lastErrorTime = null);

    /// <summary>
    ///     更新扩展的异常标记与原因。
    /// </summary>
    Task UpdateAnomalyStateAsync(Guid extensionId, bool isAnomaly, AnomalyReason? anomalyReason);

    /// <summary>
    ///     追加一条修改记录到 <see cref="UrbanWeighingExtension" />
    ///     <c>ExtraProperties["EditHistory"]</c>。
    /// </summary>
    /// <param name="extensionId">扩展实体 ID</param>
    /// <param name="before">修改前快照</param>
    /// <param name="after">修改后快照</param>
    /// <param name="source">修改来源，默认为客户端</param>
    /// <param name="isImagesModified">是否修改过图片（预留字段）</param>
    Task AppendEditEntryAsync(
        Guid extensionId,
        EditEntrySnapshot before,
        EditEntrySnapshot after,
        EditSource source = EditSource.Client,
        bool isImagesModified = false);
}
