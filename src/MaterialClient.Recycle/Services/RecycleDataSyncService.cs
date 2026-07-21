using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Utils;
using MaterialClient.Recycle.Api;
using MaterialClient.Recycle.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Recycle.Services;

/// <summary>
///     Recycle 数据上报同步核心服务（Waybill 级）。
///     扫描 WeighingMode=Recycle 且 OrderType=Completed 且 IsPendingSync=true 的 Waybill，
///     按 DeliveryType 分流：Sending→§2.2（productTransportRecord），Receiving→§2.3（materialTransportRecord）。
///     同步状态使用 Waybill 既有字段（IsPendingSync / LastSyncTime），与 SolidWaste 链路一致。
///     业务上报失败后将 WaybillId 写入 ABP Cache，默认冷却 60 分钟内跳过重试。
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

    /// <summary>出场侧照片类型（§2.2 outPhotos 聚合出场侧）</summary>
    private static readonly AttachType[] ExitPhotoTypes =
    {
        AttachType.ExitPhoto
    };

    /// <summary>收货照片类型（§2.2 receivingProof 数据源）</summary>
    private static readonly AttachType[] ReceivingProofTypes =
    {
        AttachType.TicketPhoto
    };

    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IRepository<WaybillAttachment, int> _waybillAttachmentRepository;
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;
    private readonly IRepository<WaybillMaterial, int> _waybillMaterialRepository;
    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<Provider, int> _providerRepository;
    private readonly IRepository<RecycleWaybillExtension, Guid> _recycleWaybillExtensionRepository;
    private readonly IRecycleDataApi _recycleApi;
    private readonly RecycleSyncOptions _options;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IDistributedCache<RecycleSyncFailCacheItem, long> _failCache;

    public RecycleDataSyncService(
        IRepository<Waybill, long> waybillRepository,
        IRepository<WaybillAttachment, int> waybillAttachmentRepository,
        IRepository<AttachmentFile, int> attachmentFileRepository,
        IRepository<WaybillMaterial, int> waybillMaterialRepository,
        IRepository<Material, int> materialRepository,
        IRepository<Provider, int> providerRepository,
        IRepository<RecycleWaybillExtension, Guid> recycleWaybillExtensionRepository,
        IRecycleDataApi recycleApi,
        IOptions<RecycleSyncOptions> options,
        IUnitOfWorkManager unitOfWorkManager,
        IDistributedCache<RecycleSyncFailCacheItem, long> failCache)
    {
        _waybillRepository = waybillRepository;
        _waybillAttachmentRepository = waybillAttachmentRepository;
        _attachmentFileRepository = attachmentFileRepository;
        _waybillMaterialRepository = waybillMaterialRepository;
        _materialRepository = materialRepository;
        _providerRepository = providerRepository;
        _recycleWaybillExtensionRepository = recycleWaybillExtensionRepository;
        _recycleApi = recycleApi;
        _options = options.Value;
        _unitOfWorkManager = unitOfWorkManager;
        _failCache = failCache;
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

        var toSync = new List<Waybill>(pendingWaybills.Count);
        var cooledCount = 0;
        foreach (var waybill in pendingWaybills)
        {
            if (await _failCache.GetAsync(waybill.Id, token: cancellationToken) != null)
            {
                cooledCount++;
                continue;
            }

            toSync.Add(waybill);
        }

        if (toSync.Count == 0)
        {
            if (cooledCount > 0)
            {
                Logger.LogDebug("Recycle 同步扫描：{CooledCount} 条在失败冷却缓存中，本轮跳过。", cooledCount);
            }

            return;
        }

        Logger.LogInformation(
            "Recycle 同步扫描：发现 {Count} 条待上报 Waybill（冷却跳过 {CooledCount}）。",
            toSync.Count, cooledCount);

        foreach (var waybill in toSync)
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
                await SubmitSendingAsync(waybill, materialName, carrierCompanyName, cancellationToken);
            }
            else if (waybill.DeliveryType == DeliveryType.Receiving)
            {
                await SubmitReceivingAsync(waybill, materialName, carrierCompanyName, cancellationToken);
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
    ///     §2.2 发料上报（Sending）：outPhotos 聚合进场+出场照片（进场在前），透传 UnitPrice/SaleContractNo/
    ///     ReceivingTime（来自 RecycleWaybillExtension）、ReceivingProof（TicketPhoto）、ConsigneeAddress（Provider.Address）。
    /// </summary>
    private async Task SubmitSendingAsync(
        Waybill waybill, string materialName, string? carrierCompanyName,
        CancellationToken cancellationToken)
    {
        // §2.2 outPhotos：进场侧 + 出场侧（进场在前、出场在后）。
        var entryPhotos = await BuildEntryPhotosBase64Async(waybill.Id, cancellationToken);
        var exitPhotos = await BuildExitPhotosBase64Async(waybill.Id, cancellationToken);
        var outPhotos = MergeEntryAndExitPhotos(entryPhotos, exitPhotos);

        // §2.2 扩展字段：RecycleWaybillExtension（UnitPrice/SaleContractNo/ReceivingTime）。
        var extension = await GetRecycleWaybillExtensionAsync(waybill.Id, cancellationToken);

        // §2.2 consigneeAddress：关联 Provider.Address。
        var consigneeAddress = await ResolveConsigneeAddressAsync(waybill, cancellationToken);

        // §2.2 receivingProof：TicketPhoto 收货照片 Base64。
        var receivingProof = await BuildReceivingProofBase64Async(waybill.Id, cancellationToken);

        var payload = RecycleTransportRecord.FromWaybill(
            waybill,
            outPhotos,
            materialName,
            _options.PointNumber,
            carrierCompanyName,
            unitPrice: extension?.UnitPrice,
            saleContractNo: extension?.SaleContractNo,
            receivingTime: extension?.ReceivingTime,
            receivingProof: receivingProof,
            consigneeAddress: consigneeAddress);

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
    ///     §2.3 收料上报（Receiving）：inPhoto 仅进场侧照片（不含 ExitPhoto），重量已是吨直接使用。
    /// </summary>
    private async Task SubmitReceivingAsync(
        Waybill waybill, string materialName, string? carrierCompanyName,
        CancellationToken cancellationToken)
    {
        // §2.3 inPhoto：仅进场侧照片，SHALL NOT 包含 ExitPhoto。
        var photos = await BuildEntryPhotosBase64Async(waybill.Id, cancellationToken);

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
        // 保持 IsPendingSync=true；写入 ABP Cache，冷却期内跳过上报。
        var cooldownMinutes = _options.FailCooldownMinutes <= 0 ? 60 : _options.FailCooldownMinutes;
        await _failCache.SetAsync(
            waybill.Id,
            new RecycleSyncFailCacheItem { FailMsg = failMsg },
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(cooldownMinutes)
            },
            token: cancellationToken);

        Logger.LogWarning(
            "Recycle Waybill {WaybillId} 上报失败（FailMsg={FailMsg}），保持 IsPendingSync，冷却缓存 {CooldownMinutes} 分钟后再试。",
            waybill.Id, failMsg, cooldownMinutes);

        await _waybillRepository.UpdateAsync(waybill, cancellationToken: cancellationToken);
    }

    /// <summary>
    ///     5.4: 读取 Waybill 关联附件，取进场侧照片（EntryPhoto → UnmatchedEntryPhoto → Lpr），Base64 编码逗号分隔。
    /// </summary>
    private Task<string> BuildEntryPhotosBase64Async(long waybillId, CancellationToken cancellationToken)
        => BuildPhotosBase64Async(waybillId, EntryPhotoTypes, cancellationToken);

    /// <summary>
    ///     §2.2: 读取 Waybill 关联的出场侧照片（ExitPhoto），Base64 编码逗号分隔。进场侧在前、出场侧在后由调用方聚合。
    /// </summary>
    private Task<string> BuildExitPhotosBase64Async(long waybillId, CancellationToken cancellationToken)
        => BuildPhotosBase64Async(waybillId, ExitPhotoTypes, cancellationToken);

    /// <summary>
    ///     读取 Waybill 关联附件中指定 <paramref name="types" /> 的照片，Base64 编码（无 Data URL 前缀）逗号分隔。
    ///     文件缺失记 LogWarning 并跳过（不中断同步流程）。
    /// </summary>
    private async Task<string> BuildPhotosBase64Async(
        long waybillId,
        AttachType[] types,
        CancellationToken cancellationToken)
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
            .Where(f => attachmentIds.Contains(f.Id) && types.Contains(f.AttachType))
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
    ///     §2.2: 读取 Waybill 关联的 TicketPhoto 收货照片，Base64 编码（无 Data URL 前缀）。
    ///     无关联附件或文件缺失时返回 null（记日志但不中断上报）。
    /// </summary>
    private async Task<string?> BuildReceivingProofBase64Async(long waybillId, CancellationToken cancellationToken)
    {
        var linkQueryable = await _waybillAttachmentRepository.GetQueryableAsync();
        var attachmentIds = await linkQueryable
            .Where(l => l.WaybillId == waybillId)
            .Select(l => l.AttachmentFileId)
            .ToListAsync(cancellationToken);

        if (attachmentIds.Count == 0)
            return null;

        var fileQueryable = await _attachmentFileRepository.GetQueryableAsync();
        var file = await fileQueryable
            .Where(f => attachmentIds.Contains(f.Id) && ReceivingProofTypes.Contains(f.AttachType))
            .OrderBy(f => f.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (file == null)
            return null;

        var absolutePath = PathManager.ToAbsolutePath(file.LocalPath);
        if (!File.Exists(absolutePath))
        {
            Logger.LogWarning("Recycle 收货照片文件缺失，跳过：WaybillId={WaybillId}, Path={Path}", waybillId, absolutePath);
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(absolutePath, cancellationToken);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    ///     合并进场侧与出场侧照片 Base64，进场侧在前、出场侧在后，英文逗号分隔；两侧均为空时返回空串。
    /// </summary>
    private static string MergeEntryAndExitPhotos(string entryPhotos, string exitPhotos)
    {
        var hasEntry = !string.IsNullOrEmpty(entryPhotos);
        var hasExit = !string.IsNullOrEmpty(exitPhotos);
        if (hasEntry && hasExit) return $"{entryPhotos},{exitPhotos}";
        if (hasEntry) return entryPhotos;
        if (hasExit) return exitPhotos;
        return string.Empty;
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

    /// <summary>
    ///     §2.2: 按 WaybillId 读取 RecycleWaybillExtension（UnitPrice/SaleContractNo/ReceivingTime）。无记录返回 null。
    /// </summary>
    private async Task<RecycleWaybillExtension?> GetRecycleWaybillExtensionAsync(
        long waybillId, CancellationToken cancellationToken)
    {
        var queryable = await _recycleWaybillExtensionRepository.GetQueryableAsync();
        return await queryable
            .Where(e => e.WaybillId == waybillId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    ///     §2.2: 解析 consigneeAddress（ProviderId → Provider.Address，本地专用字段）。
    ///     Provider 不存在或 Address 为空返回 null。
    /// </summary>
    private async Task<string?> ResolveConsigneeAddressAsync(Waybill waybill, CancellationToken cancellationToken)
    {
        if (!waybill.ProviderId.HasValue)
            return null;

        var provider = await _providerRepository.FindAsync(waybill.ProviderId.Value, cancellationToken: cancellationToken);
        return string.IsNullOrWhiteSpace(provider?.Address) ? null : provider.Address;
    }
}
