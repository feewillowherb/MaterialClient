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
///     Recycle 数据上报同步核心服务（Waybill 级）。
///     扫描 WeighingMode=Recycle 且 OrderType=Completed 且 IsPendingSync=true 的 Waybill，
///     按 DeliveryType 分流：Sending→§2.2（productTransportRecord），Receiving→§2.3（materialTransportRecord）。
///     同步状态使用 Waybill 既有字段（IsPendingSync / LastSyncTime），与 SolidWaste 链路一致。
/// </summary>
public class RecycleDataSyncService : DomainService
{
    /// <summary>进场侧照片类型优先级（EntryPhoto → UnmatchedEntryPhoto → Lpr）</summary>
    private static readonly AttachType[] EntryPhotoTypes =
    {
        AttachType.EntryPhoto,
        AttachType.UnmatchedEntryPhoto,
        AttachType.Lpr
    };

    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IRepository<WaybillAttachment, int> _waybillAttachmentRepository;
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;
    private readonly IRepository<WaybillMaterial, int> _waybillMaterialRepository;
    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<Provider, int> _providerRepository;
    private readonly IRecycleDataApi _recycleApi;
    private readonly RecycleSyncOptions _options;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public RecycleDataSyncService(
        IRepository<Waybill, long> waybillRepository,
        IRepository<WaybillAttachment, int> waybillAttachmentRepository,
        IRepository<AttachmentFile, int> attachmentFileRepository,
        IRepository<WaybillMaterial, int> waybillMaterialRepository,
        IRepository<Material, int> materialRepository,
        IRepository<Provider, int> providerRepository,
        IRecycleDataApi recycleApi,
        IOptions<RecycleSyncOptions> options,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _waybillRepository = waybillRepository;
        _waybillAttachmentRepository = waybillAttachmentRepository;
        _attachmentFileRepository = attachmentFileRepository;
        _waybillMaterialRepository = waybillMaterialRepository;
        _materialRepository = materialRepository;
        _providerRepository = providerRepository;
        _recycleApi = recycleApi;
        _options = options.Value;
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>
    ///     执行一轮同步：扫描已完成 Waybill 并上报。
    /// </summary>
    public async Task SyncOnceAsync(CancellationToken cancellationToken = default)
    {
        var pendingWaybills = await GetPendingWaybillsAsync(cancellationToken);
        if (pendingWaybills.Count == 0)
        {
            return;
        }

        Logger.LogInformation("Recycle 同步扫描：发现 {Count} 条待上报 Waybill。", pendingWaybills.Count);

        foreach (var waybill in pendingWaybills)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ProcessWaybillAsync(waybill, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Recycle 同步处理 Waybill {WaybillId} 时发生未预期异常。", waybill.Id);
            }
        }
    }

    /// <summary>
    ///     查询待上报 Waybill：WeighingMode=Recycle，OrderType=Completed，IsPendingSync=true。
    /// </summary>
    private async Task<List<Waybill>> GetPendingWaybillsAsync(CancellationToken cancellationToken)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);

        var queryable = await _waybillRepository.GetQueryableAsync();
        var waybills = await queryable
            .Where(w => w.WeighingMode == WeighingMode.Recycle)
            .Where(w => w.OrderType == OrderTypeEnum.Completed)
            .Where(w => w.IsPendingSync)
            .ToListAsync(cancellationToken);

        await uow.CompleteAsync(cancellationToken);
        return waybills;
    }

    private async Task ProcessWaybillAsync(Waybill waybill, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 5.6: OrderNo 为空时跳过上报
        if (string.IsNullOrWhiteSpace(waybill.OrderNo))
        {
            Logger.LogWarning("Recycle Waybill {WaybillId} 的 OrderNo 为空，跳过上报。", waybill.Id);
            return;
        }

        // 解析 Material.Name（productName / materialName）
        var materialName = await ResolveMaterialNameAsync(waybill, cancellationToken);
        if (string.IsNullOrWhiteSpace(materialName))
        {
            Logger.LogWarning("Recycle Waybill {WaybillId} 无法解析 Material.Name，跳过上报。", waybill.Id);
            return;
        }

        // 解析 carrierCompanyName
        var carrierCompanyName = await ResolveCarrierCompanyNameAsync(waybill, cancellationToken);

        // 5.4: 照片取进场侧（聚合 Waybill 关联附件）
        var photos = await BuildEntryPhotosBase64Async(waybill.Id, cancellationToken);

        // 净重非正跳过
        if ((waybill.OrderGoodsWeight ?? 0m) <= 0m)
        {
            Logger.LogWarning("Recycle Waybill {WaybillId} 净重非正（{Weight} kg），跳过上报。", waybill.Id, waybill.OrderGoodsWeight);
            return;
        }

        // 5.3: 按 DeliveryType 分流
        try
        {
            if (waybill.DeliveryType == DeliveryType.Sending)
            {
                await SubmitSendingAsync(waybill, photos, materialName, carrierCompanyName, cancellationToken);
            }
            else if (waybill.DeliveryType == DeliveryType.Receiving)
            {
                await SubmitReceivingAsync(waybill, photos, materialName, carrierCompanyName, cancellationToken);
            }
            else
            {
                Logger.LogWarning("Recycle Waybill {WaybillId} 的 DeliveryType 为 {DeliveryType}，无法分流，跳过。", waybill.Id, waybill.DeliveryType);
            }
        }
        catch (HttpRequestException ex)
        {
            Logger.LogWarning(ex, "Recycle 上报 Waybill {WaybillId} 网络异常，本轮跳过（不计失败次数）。", waybill.Id);
        }
    }

    /// <summary>
    ///     §2.2 发料上报（Sending）：重量已是吨，直接使用，出场照片。
    /// </summary>
    private async Task SubmitSendingAsync(
        Waybill waybill, string photos, string materialName, string? carrierCompanyName,
        CancellationToken cancellationToken)
    {
        // §2.2 出场照片（取进场侧：设计定稿统一取进场侧附件）
        var payload = RecycleTransportRecord.FromWaybill(
            waybill,
            photos,
            materialName,
            _options.PointNumber,
            carrierCompanyName);

        RecycleApiResponse? response;
        try
        {
            response = await _recycleApi.SubmitTransportRecordAsync(new List<RecycleTransportRecord> { payload }, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw;
        }

        Logger.LogInformation(
            "Recycle §2.2 payload: WaybillId={WaybillId}, Payload={@Payload}",
            waybill.Id, payload.ForLogging());

        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var now = DateTime.UtcNow;

        if (response != null && response.Code == 200)
        {
            waybill.ResetPendingSync(now);
            await _waybillRepository.UpdateAsync(waybill, cancellationToken: cancellationToken);
            Logger.LogInformation("Recycle Waybill {WaybillId} §2.2 上报成功（DataNo={DataNo}）。", waybill.Id, payload.DataNo);
        }
        else
        {
            await HandleFailureAsync(waybill, response, cancellationToken);
        }

        await uow.CompleteAsync(cancellationToken);
    }

    /// <summary>
    ///     §2.3 收料上报（Receiving）：重量吨→kg（×1000），进场照片。
    /// </summary>
    private async Task SubmitReceivingAsync(
        Waybill waybill, string photos, string materialName, string? carrierCompanyName,
        CancellationToken cancellationToken)
    {
        var payload = RecycleMaterialTransportRecord.FromWaybill(
            waybill,
            photos,
            materialName,
            carrierCompanyName,
            _options.PointNumber);

        RecycleApiResponse? response;
        try
        {
            response = await _recycleApi.SubmitMaterialTransportRecordAsync(new List<RecycleMaterialTransportRecord> { payload }, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw;
        }

        Logger.LogInformation(
            "Recycle §2.3 payload: WaybillId={WaybillId}, Payload={@Payload}",
            waybill.Id, payload.ForLogging());

        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var now = DateTime.UtcNow;

        if (response != null && response.Code == 200)
        {
            waybill.ResetPendingSync(now);
            await _waybillRepository.UpdateAsync(waybill, cancellationToken: cancellationToken);
            Logger.LogInformation("Recycle Waybill {WaybillId} §2.3 上报成功（DataNo={DataNo}）。", waybill.Id, payload.DataNo);
        }
        else
        {
            await HandleFailureAsync(waybill, response, cancellationToken);
        }

        await uow.CompleteAsync(cancellationToken);
    }

    private async Task HandleFailureAsync(Waybill waybill, RecycleApiResponse? response, CancellationToken cancellationToken)
    {
        var failMsg = response?.Msg ?? $"HTTP business failure (code={response?.Code})";
        // 失败时保持 IsPendingSync=true，下轮继续重试；仅记录日志。
        Logger.LogWarning("Recycle Waybill {WaybillId} 上报失败（FailMsg={FailMsg}），保持 IsPendingSync，下轮重试。",
            waybill.Id, failMsg);

        await _waybillRepository.UpdateAsync(waybill, cancellationToken: cancellationToken);
    }

    /// <summary>
    ///     5.4: 读取 Waybill 关联附件，取进场侧照片（EntryPhoto → UnmatchedEntryPhoto → Lpr），Base64 编码逗号分隔。
    /// </summary>
    private async Task<string> BuildEntryPhotosBase64Async(long waybillId, CancellationToken cancellationToken)
    {
        var linkQueryable = await _waybillAttachmentRepository.GetQueryableAsync();
        var attachmentIds = await linkQueryable
            .Where(l => l.WaybillId == waybillId)
            .Select(l => l.AttachmentFileId)
            .ToListAsync(cancellationToken);

        if (attachmentIds.Count == 0)
            return string.Empty;

        var fileQueryable = await _attachmentFileRepository.GetQueryableAsync();
        var files = await fileQueryable
            .Where(f => attachmentIds.Contains(f.Id) && EntryPhotoTypes.Contains(f.AttachType))
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

    /// <summary>
    ///     5.5: 解析 Material.Name（productName / materialName）。
    ///     优先级：WaybillMaterial → Waybill.MaterialId。
    /// </summary>
    private async Task<string?> ResolveMaterialNameAsync(Waybill waybill, CancellationToken cancellationToken)
    {
        int? materialId = null;

        var waybillMaterialQueryable = await _waybillMaterialRepository.GetQueryableAsync();
        materialId = await waybillMaterialQueryable
            .Where(wm => wm.WaybillId == waybill.Id)
            .OrderBy(wm => wm.Id)
            .Select(wm => (int?)wm.MaterialId)
            .FirstOrDefaultAsync(cancellationToken);

        materialId ??= waybill.MaterialId;

        if (!materialId.HasValue)
            return null;

        var material = await _materialRepository.FindAsync(materialId.Value, cancellationToken: cancellationToken);
        return string.IsNullOrWhiteSpace(material?.Name) ? null : material.Name;
    }

    /// <summary>
    ///     5.5: 解析 carrierCompanyName（ProviderId → Provider.ProviderName）。
    /// </summary>
    private async Task<string?> ResolveCarrierCompanyNameAsync(Waybill waybill, CancellationToken cancellationToken)
    {
        if (!waybill.ProviderId.HasValue)
            return null;

        var provider = await _providerRepository.FindAsync(waybill.ProviderId.Value, cancellationToken: cancellationToken);
        return provider?.ProviderName;
    }
}
