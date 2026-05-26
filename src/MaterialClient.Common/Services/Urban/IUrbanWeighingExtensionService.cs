using MaterialClient.Common.Entities;
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
    Task<UrbanWeighingExtension> CreateForRecordAsync(long weighingRecordId);

    /// <summary>
    ///     按称重记录 Id 查询扩展。
    /// </summary>
    Task<UrbanWeighingExtension?> GetByWeighingRecordIdAsync(long weighingRecordId);

    /// <summary>
    ///     分页查询 Urban 称重记录并关联扩展（LEFT JOIN 语义，扩展挂到 <see cref="WeighingRecord.UrbanExtension" />）。
    /// </summary>
    Task<PagedResultDto<WeighingRecord>> GetPagedWithRecordsAsync(
        int pageIndex,
        int pageSize,
        string? tabFilter,
        string? searchText,
        DateTime? startTime,
        DateTime? endTime);

    /// <summary>
    ///     查询待上传的扩展行（后台同步 worker 使用）。
    /// </summary>
    Task<List<UrbanWeighingExtension>> GetPendingForUploadAsync(int maxCount = 100);

    /// <summary>
    ///     更新扩展同步状态。
    /// </summary>
    Task UpdateSyncStatusAsync(Guid extensionId, SyncStatus syncStatus, DateTime? lastErrorTime = null);

    /// <summary>
    ///     更新扩展的异常标记。
    /// </summary>
    Task UpdateAnomalyFlagAsync(Guid extensionId, bool isAnomaly);
}
