using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Utils;
using MaterialClient.Recycle.Api;
using MaterialClient.Recycle.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Recycle.Services;

/// <summary>
///     Recycle 数据上报同步核心服务。
///     定时扫描 <see cref="WeighingMode.Recycle" /> 的称重记录，按 §2.2 接口要求上报：
///     附件 Base64 内嵌（不带标识头、逗号分隔）、重量 kg→吨、JSON Array 批量提交、HMAC-SHA256 由 DelegatingHandler 注入。
///     同步状态承载于 <see cref="WeighingRecord.ExtraProperties" />（见 <see cref="RecycleSyncStateStore" />）。
/// </summary>
public class RecycleDataSyncService : DomainService
{
    /// <summary>作为 outPhotos 上报的附件类型集合（见变更说明：§2.2 文档的 LprCapturePhoto 无对应枚举值，回退为出场/抓拍照片）。</summary>
    private static readonly AttachType[] OutPhotoTypes =
    {
        AttachType.ExitPhoto,
        AttachType.Lpr,
        AttachType.UrbanPhoto
    };

    private readonly IRepository<WeighingRecord, long> _recordRepository;
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IRepository<WaybillMaterial, int> _waybillMaterialRepository;
    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<WeighingRecordAttachment, int> _attachmentLinkRepository;
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;
    private readonly IRecycleDataApi _recycleApi;
    private readonly RecycleSyncOptions _options;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public RecycleDataSyncService(
        IRepository<WeighingRecord, long> recordRepository,
        IRepository<Waybill, long> waybillRepository,
        IRepository<WaybillMaterial, int> waybillMaterialRepository,
        IRepository<Material, int> materialRepository,
        IRepository<WeighingRecordAttachment, int> attachmentLinkRepository,
        IRepository<AttachmentFile, int> attachmentFileRepository,
        IRecycleDataApi recycleApi,
        IOptions<RecycleSyncOptions> options,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _recordRepository = recordRepository;
        _waybillRepository = waybillRepository;
        _waybillMaterialRepository = waybillMaterialRepository;
        _materialRepository = materialRepository;
        _attachmentLinkRepository = attachmentLinkRepository;
        _attachmentFileRepository = attachmentFileRepository;
        _recycleApi = recycleApi;
        _options = options.Value;
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>
    ///     执行一轮同步扫描：查询未同步记录并对每条执行上报流程。
    /// </summary>
    public async Task SyncOnceAsync(CancellationToken cancellationToken = default)
    {
        var pendingIds = await GetPendingRecordIdsAsync(cancellationToken);
        if (pendingIds.Count == 0)
        {
            return;
        }

        Logger.LogInformation("Recycle 同步扫描：发现 {Count} 条待上报记录。", pendingIds.Count);

        foreach (var recordId in pendingIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ProcessRecordAsync(recordId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Recycle 同步处理记录 {RecordId} 时发生未预期异常。", recordId);
            }
        }
    }

    /// <summary>
    ///     查询待上报记录 Id 列表：WeighingMode==Recycle 且未同步、未放弃重试。
    /// </summary>
    private async Task<List<long>> GetPendingRecordIdsAsync(CancellationToken cancellationToken)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);

        var queryable = await _recordRepository.GetQueryableAsync();
        var records = await queryable
            .Where(r => r.WeighingMode == WeighingMode.Recycle)
            .ToListAsync(cancellationToken);

        await uow.CompleteAsync(cancellationToken);

        var maxFail = _options.MaxFailCount;
        return records
            .Where(r =>
            {
                var status = RecycleSyncStateStore.GetSyncStatus(r);
                if (status == SyncStatus.Synced)
                {
                    return false;
                }

                // Pending 或重试中的 Failed：仅当 FailCount < MaxFailCount 才继续尝试。
                return RecycleSyncStateStore.GetFailCount(r) < maxFail;
            })
            .Select(r => r.Id)
            .ToList();
    }

    private async Task ProcessRecordAsync(long recordId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);

        var record = await _recordRepository.GetAsync(recordId, cancellationToken: cancellationToken);
        if (record == null)
        {
            await uow.CompleteAsync(cancellationToken);
            return;
        }

        // 已被其它环节同步/放弃，跳过。
        var currentStatus = RecycleSyncStateStore.GetSyncStatus(record);
        if (currentStatus == SyncStatus.Synced)
        {
            await uow.CompleteAsync(cancellationToken);
            return;
        }

        var waybill = await LoadWaybillAsync(record.WaybillId, cancellationToken);
        var productName = await ResolveProductNameAsync(record, waybill, cancellationToken);
        if (string.IsNullOrWhiteSpace(productName))
        {
            Logger.LogWarning("Recycle 记录 {RecordId} 无法解析 Material.Name，跳过上报。", recordId);
            await uow.CompleteAsync(cancellationToken);
            return;
        }

        // 构造请求（含附件 Base64）。配置缺失（密钥）会从 HMAC Handler 抛 InvalidOperationException。
        RecycleTransportRecord payload;
        try
        {
            var outPhotos = await BuildOutPhotosBase64Async(record.Id, cancellationToken);
            payload = RecycleTransportRecord.FromWeighingRecord(
                record, waybill, outPhotos, productName, _options.PointNumber);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Recycle 构造记录 {RecordId} 上报负载失败，跳过本轮。", recordId);
            await uow.CompleteAsync(cancellationToken);
            return;
        }

        // 净重非正：§2.2 不应上报该记录，跳过（不计数）。
        if (payload.NetWeight <= 0)
        {
            Logger.LogWarning("Recycle 记录 {RecordId} 净重非正（{NetWeight} 吨），跳过上报。", recordId, payload.NetWeight);
            await uow.CompleteAsync(cancellationToken);
            return;
        }

        // 调用 §2.2 接口（JSON Array 批量提交）。
        RecycleApiResponse? response;
        try
        {
            response = await _recycleApi.SubmitTransportRecordAsync(new List<RecycleTransportRecord> { payload }, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // 网络异常：不计 FailCount，不改状态，下次重试。
            Logger.LogWarning(ex, "Recycle 上报记录 {RecordId} 网络异常，本轮跳过（不计失败次数）。", recordId);
            await uow.CompleteAsync(cancellationToken);
            return;
        }

        var now = DateTime.UtcNow;

        if (response != null && response.Code == 200)
        {
            RecycleSyncStateStore.SetSynced(record, now);
            await _recordRepository.UpdateAsync(record, cancellationToken: cancellationToken);
            Logger.LogInformation("Recycle 记录 {RecordId} 上报成功（DataNo={DataNo}）。", recordId, payload.DataNo);
        }
        else
        {
            // 业务失败：FailCount++，记录 FailMsg；达到 MaxFailCount 则放弃。
            var failCount = RecycleSyncStateStore.GetFailCount(record) + 1;
            var failMsg = response?.Msg ?? $"HTTP business failure (code={response?.Code})";
            RecycleSyncStateStore.SetFailed(record, failCount, failMsg, now);

            if (failCount >= _options.MaxFailCount)
            {
                RecycleSyncStateStore.MarkAbandoned(record);
                Logger.LogWarning("Recycle 记录 {RecordId} 达到最大失败次数 {MaxFail}，放弃重试（FailMsg={FailMsg}）。",
                    recordId, _options.MaxFailCount, failMsg);
            }
            else
            {
                Logger.LogWarning("Recycle 记录 {RecordId} 上报失败（FailCount={FailCount}/{MaxFail}，FailMsg={FailMsg}）。",
                    recordId, failCount, _options.MaxFailCount, failMsg);
            }

            await _recordRepository.UpdateAsync(record, cancellationToken: cancellationToken);
        }

        await uow.CompleteAsync(cancellationToken);
    }

    private async Task<Waybill?> LoadWaybillAsync(long? waybillId, CancellationToken cancellationToken)
    {
        if (!waybillId.HasValue)
        {
            return null;
        }

        return await _waybillRepository.FindAsync(waybillId.Value, cancellationToken: cancellationToken);
    }

    /// <summary>
    ///     解析 §2.2 <c>productName</c>：取关联物料 <see cref="Material.Name"/>。
    ///     MaterialId 优先级：运单物料行 → 运单 MaterialId → 称重记录 MaterialsJson 首项。
    /// </summary>
    private async Task<string?> ResolveProductNameAsync(
        WeighingRecord record,
        Waybill? waybill,
        CancellationToken cancellationToken)
    {
        int? materialId = null;

        if (waybill != null)
        {
            var waybillMaterialQueryable = await _waybillMaterialRepository.GetQueryableAsync();
            materialId = await waybillMaterialQueryable
                .Where(wm => wm.WaybillId == waybill.Id)
                .OrderBy(wm => wm.Id)
                .Select(wm => (int?)wm.MaterialId)
                .FirstOrDefaultAsync(cancellationToken);

            materialId ??= waybill.MaterialId;
        }

        materialId ??= record.Materials.FirstOrDefault()?.MaterialId;

        if (!materialId.HasValue)
        {
            return null;
        }

        var material = await _materialRepository.FindAsync(materialId.Value, cancellationToken: cancellationToken);
        return string.IsNullOrWhiteSpace(material?.Name) ? null : material.Name;
    }

    /// <summary>
    ///     读取记录关联的出场/抓拍附件，Base64 编码（不带标识头），多张英文逗号分隔。
    /// </summary>
    private async Task<string> BuildOutPhotosBase64Async(long recordId, CancellationToken cancellationToken)
    {
        var linkQueryable = await _attachmentLinkRepository.GetQueryableAsync();
        var attachmentIds = await linkQueryable
            .Where(l => l.WeighingRecordId == recordId)
            .Select(l => l.AttachmentFileId)
            .ToListAsync(cancellationToken);

        if (attachmentIds.Count == 0)
        {
            return string.Empty;
        }

        var fileQueryable = await _attachmentFileRepository.GetQueryableAsync();
        var files = await fileQueryable
            .Where(f => attachmentIds.Contains(f.Id) && OutPhotoTypes.Contains(f.AttachType))
            .ToListAsync(cancellationToken);

        var base64List = new List<string>(files.Count);
        foreach (var file in files)
        {
            var absolutePath = PathManager.ToAbsolutePath(file.LocalPath);
            if (!File.Exists(absolutePath))
            {
                Logger.LogWarning("Recycle 附件文件缺失，跳过该图片：{Path}", absolutePath);
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(absolutePath, cancellationToken);
            base64List.Add(Convert.ToBase64String(bytes));
        }

        return string.Join(",", base64List);
    }
}
