using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using MaterialClient.Common.Utils;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

/// <summary>
///     Recycle 已提交收货详情（供收货对话框回填）。
/// </summary>
public record RecycleReceivingDetail(DateTime? ReceivingTime, string? ImagePath);

/// <summary>
///     Recycle 模式收货领域服务接口。
///     收货为独立动作（不并入 <see cref="IRecycleWeighingService.UpdateRecycleModeAsync" />）：
///     录入收货时间 + 收货照片（TicketPhoto），持久化到 RecycleWaybillExtension 与 WaybillAttachment，并标记 Waybill 待上报。
/// </summary>
public interface IRecycleReceivingService
{
    /// <summary>
    ///     确认收货：落盘收货照片为 TicketPhoto 附件、关联 Waybill、写入收货时间、标记已收货与待上报。
    /// </summary>
    /// <param name="waybillId">运单 Id</param>
    /// <param name="receivingTime">收货时间</param>
    /// <param name="imageStream">收货照片流（非空）</param>
    Task ConfirmAsync(long waybillId, DateTime receivingTime, Stream imageStream);

    /// <summary>
    ///     读取已提交收货信息（时间 + TicketPhoto 本地绝对路径），供收货对话框回填。
    /// </summary>
    Task<RecycleReceivingDetail> GetDetailAsync(long waybillId);
}

/// <summary>
///     Recycle 模式收货领域服务实现。
///     经 ABP 约定扫描注册（DomainService + ITransientDependency + AutoConstructor），SHALL NOT 在 Module 显式注册。
///     写入方法标注 <see cref="UnitOfWorkAttribute" />，异常时事务回滚（不残留半成品附件关联）。
/// </summary>
[AutoConstructor]
public partial class RecycleReceivingService : DomainService, IRecycleReceivingService, ITransientDependency
{
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly IRecycleWaybillExtensionStore _recycleWaybillExtensionStore;
    private readonly IAttachmentService _attachmentService;
    private readonly ILogger<RecycleReceivingService>? _logger;

    /// <inheritdoc />
    [UnitOfWork]
    public virtual async Task ConfirmAsync(long waybillId, DateTime receivingTime, Stream imageStream)
    {
        if (imageStream == null)
        {
            throw new ArgumentException("Receiving proof image stream is required.", nameof(imageStream));
        }

        var waybill = await _waybillRepository.GetAsync(waybillId);

        // 1. 落盘收货照片为 TicketPhoto 附件并关联 Waybill（经 AttachmentService，复用 TicketPhoto 替换语义）。
        var now = DateTime.Now;
        var storageDir = AttachmentPathUtils.GetLocalStorageAbsolutePath(AttachType.TicketPhoto, now);
        Directory.CreateDirectory(storageDir);
        var fileName = AttachmentPathUtils.GenerateBillPhotoFileName(now);
        var savedPath = Path.Combine(storageDir, fileName);
        await using (var fs = File.Create(savedPath))
        {
            await imageStream.CopyToAsync(fs);
        }

        var listItem = new WeighingListItemDto
        {
            Id = waybillId,
            ItemType = WeighingListItemType.Waybill
        };
        await _attachmentService.CreateOrReplaceBillPhotoAsync(listItem, savedPath);

        // 2. 按 WaybillId upsert RecycleWaybillExtension（ReceivingTime）。
        await UpsertReceivingAsync(waybillId, receivingTime);

        // 3. 标记 Waybill 待上报，使后台 RecycleDataSyncService 下轮采集 receivingTime/receivingProof 上报 §2.2。
        waybill.SetPendingSync();
        await _waybillRepository.UpdateAsync(waybill);

        _logger?.LogInformation(
            "Recycle 收货完成：WaybillId={WaybillId}, ReceivingTime={ReceivingTime:yyyy-MM-dd HH:mm:ss}",
            waybillId, receivingTime);
    }

    /// <inheritdoc />
    [UnitOfWork]
    public virtual async Task<RecycleReceivingDetail> GetDetailAsync(long waybillId)
    {
        var extension = await _recycleWaybillExtensionStore.FindByWaybillIdAsync(waybillId);

        string? imagePath = null;
        var attachments = await _attachmentService.GetAttachmentsByWaybillIdsAsync([waybillId]);
        if (attachments.TryGetValue(waybillId, out var files))
        {
            var ticket = files.FirstOrDefault(f => f.AttachType == AttachType.TicketPhoto);
            if (ticket != null && !string.IsNullOrWhiteSpace(ticket.LocalPath))
            {
                var absolute = PathManager.ToAbsolutePath(ticket.LocalPath);
                if (File.Exists(absolute))
                {
                    imagePath = absolute;
                }
            }
        }

        return new RecycleReceivingDetail(
            extension?.ReceivingTime,
            imagePath);
    }

    /// <summary>
    ///     按 <paramref name="waybillId" /> upsert <see cref="RecycleWaybillExtension" /> 的收货字段。
    ///     存在则更新收货时间（保留既有 UnitPrice/SaleContractNo），否则新建。
    /// </summary>
    private async Task UpsertReceivingAsync(long waybillId, DateTime receivingTime)
    {
        await _recycleWaybillExtensionStore.UpsertReceivingTimeAsync(waybillId, receivingTime);
    }
}
