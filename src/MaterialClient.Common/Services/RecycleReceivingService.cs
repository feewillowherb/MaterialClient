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
///     Recycle 模式收货领域服务接口。
///     收货为独立动作（不并入 <see cref="IRecycleWeighingService.UpdateRecycleModeAsync" />）：
///     录入收货时间 + 收货照片（TicketPhoto），持久化到 RecycleWaybillExtension 与 WaybillAttachment，并标记 Waybill 待上报。
/// </summary>
public interface IRecycleReceivingService
{
    /// <summary>
    ///     确认收货：落盘收货照片为 TicketPhoto 附件、关联 Waybill、写入收货时间、标记待上报。
    /// </summary>
    /// <param name="waybillId">运单 Id</param>
    /// <param name="receivingTime">收货时间</param>
    /// <param name="imageStream">收货照片流（非空）</param>
    Task ConfirmAsync(long waybillId, DateTime receivingTime, Stream imageStream);
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
    private readonly IRepository<RecycleWaybillExtension, Guid> _recycleWaybillExtensionRepository;
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

        // 2. 按 WaybillId upsert RecycleWaybillExtension.ReceivingTime（覆盖原值，支持重复收货）。
        await UpsertReceivingTimeAsync(waybillId, receivingTime);

        // 3. 标记 Waybill 待上报，使后台 RecycleDataSyncService 下轮采集 receivingTime/receivingProof 上报 §2.2。
        waybill.SetPendingSync();
        await _waybillRepository.UpdateAsync(waybill);

        _logger?.LogInformation(
            "Recycle 收货完成：WaybillId={WaybillId}, ReceivingTime={ReceivingTime:yyyy-MM-dd HH:mm:ss}",
            waybillId, receivingTime);
    }

    /// <summary>
    ///     按 <paramref name="waybillId" /> upsert <see cref="RecycleWaybillExtension" /> 的 <see cref="RecycleWaybillExtension.ReceivingTime" />。
    ///     存在则更新收货时间（保留既有 UnitPrice/SaleContractNo），否则新建。
    /// </summary>
    private async Task UpsertReceivingTimeAsync(long waybillId, DateTime receivingTime)
    {
        var existing = await _recycleWaybillExtensionRepository
            .FirstOrDefaultAsync(e => e.WaybillId == waybillId);

        if (existing == null)
        {
            var extension = new RecycleWaybillExtension(waybillId)
            {
                ReceivingTime = receivingTime
            };
            await _recycleWaybillExtensionRepository.InsertAsync(extension);
            return;
        }

        existing.ReceivingTime = receivingTime;
        await _recycleWaybillExtensionRepository.UpdateAsync(existing);
    }
}
