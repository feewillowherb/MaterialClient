using System.Linq.Expressions;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     单测：RecycleReceivingService.ConfirmAsync 成功落盘 + 异常事务回滚（异常时不残留半成品附件关联）。
/// </summary>
public class RecycleReceivingServiceTests
{
    private static Waybill BuildWaybill(long id) =>
        new(id, $"fl-{id}") { WeighingMode = WeighingMode.Recycle };

    private static RecycleReceivingService CreateService(
        IRepository<Waybill, long> waybillRepo,
        IRepository<RecycleWaybillExtension, Guid> extensionRepo,
        IAttachmentService attachmentService)
    {
        return new RecycleReceivingService(waybillRepo, extensionRepo, attachmentService, null);
    }

    [Fact]
    public async Task ConfirmAsync_Persists_ReceivingTime_And_Marks_PendingSync()
    {
        var waybill = BuildWaybill(6001);
        var waybillRepo = Substitute.For<IRepository<Waybill, long>>();
        waybillRepo.GetAsync(6001).Returns(waybill);
        waybillRepo.UpdateAsync(Arg.Any<Waybill>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Waybill>());

        var extensionRepo = Substitute.For<IRepository<RecycleWaybillExtension, Guid>>();
        extensionRepo
            .FirstOrDefaultAsync(Arg.Any<Expression<Func<RecycleWaybillExtension, bool>>>())
            .Returns((RecycleWaybillExtension?)null);
        RecycleWaybillExtension? inserted = null;
        extensionRepo
            .InsertAsync(Arg.Any<RecycleWaybillExtension>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                inserted = ci.Arg<RecycleWaybillExtension>();
                return inserted;
            });

        var attachmentService = Substitute.For<IAttachmentService>();

        var service = CreateService(waybillRepo, extensionRepo, attachmentService);

        var receivingTime = new DateTime(2026, 7, 9, 15, 20, 0);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        await service.ConfirmAsync(6001, receivingTime, stream);

        // 落盘 TicketPhoto 附件，按 WaybillId 关联
        await attachmentService.Received(1).CreateOrReplaceBillPhotoAsync(
            Arg.Is<WeighingListItemDto>(i => i.Id == 6001 && i.ItemType == WeighingListItemType.Waybill),
            Arg.Any<string>());

        // 收货时间持久化到 RecycleWaybillExtension
        inserted.ShouldNotBeNull();
        inserted!.WaybillId.ShouldBe(6001);
        inserted.ReceivingTime.ShouldBe(receivingTime);

        // Waybill 标记待上报
        waybill.IsPendingSync.ShouldBeTrue();
    }

    [Fact]
    public async Task ConfirmAsync_When_AttachmentFails_RollsBack_No_Extension_Persisted()
    {
        var waybill = BuildWaybill(6002);
        var waybillRepo = Substitute.For<IRepository<Waybill, long>>();
        waybillRepo.GetAsync(6002).Returns(waybill);

        var extensionRepo = Substitute.For<IRepository<RecycleWaybillExtension, Guid>>();

        var attachmentService = Substitute.For<IAttachmentService>();
        // 附件落盘失败 → 整个 UnitOfWork 应回滚，后续 upsert/更新不应执行
        attachmentService
            .CreateOrReplaceBillPhotoAsync(Arg.Any<WeighingListItemDto>(), Arg.Any<string>())
            .Throws(new InvalidOperationException("disk full"));

        var service = CreateService(waybillRepo, extensionRepo, attachmentService);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        // 异常向上抛出（[UnitOfWork] 触发回滚）
        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.ConfirmAsync(6002, DateTime.Now, stream));

        // 异常时 SHALL NOT 残留半成品：扩展记录未被写入
        await extensionRepo.DidNotReceiveWithAnyArgs().InsertAsync(default!, default, default);
        await extensionRepo.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default, default);
    }
}
