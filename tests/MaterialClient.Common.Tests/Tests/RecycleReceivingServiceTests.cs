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

public class RecycleReceivingServiceTests
{
    private static Waybill BuildWaybill(long id) =>
        new(id, $"fl-{id}") { WeighingMode = WeighingMode.Recycle };

    private static RecycleReceivingService CreateService(
        IRepository<Waybill, long> waybillRepo,
        IRecycleWaybillExtensionStore store,
        IAttachmentService attachmentService)
    {
        return new RecycleReceivingService(waybillRepo, store, attachmentService, null);
    }

    [Fact]
    public async Task ConfirmAsync_Persists_ReceivingTime_And_Marks_PendingSync()
    {
        var waybill = BuildWaybill(6001);
        var waybillRepo = Substitute.For<IRepository<Waybill, long>>();
        waybillRepo.GetAsync(6001).Returns(waybill);
        waybillRepo.UpdateAsync(Arg.Any<Waybill>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Waybill>());

        var store = Substitute.For<IRecycleWaybillExtensionStore>();
        var attachmentService = Substitute.For<IAttachmentService>();
        var service = CreateService(waybillRepo, store, attachmentService);

        var receivingTime = new DateTime(2026, 7, 9, 15, 20, 0);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        await service.ConfirmAsync(6001, receivingTime, stream);

        await attachmentService.Received(1).CreateOrReplaceBillPhotoAsync(
            Arg.Is<WeighingListItemDto>(i => i.Id == 6001 && i.ItemType == WeighingListItemType.Waybill),
            Arg.Any<string>());

        await store.Received(1).UpsertReceivingTimeAsync(6001, receivingTime);
        waybill.IsPendingSync.ShouldBeTrue();
    }

    [Fact]
    public async Task ConfirmAsync_When_AttachmentFails_RollsBack_No_Extension_Persisted()
    {
        var waybill = BuildWaybill(6002);
        var waybillRepo = Substitute.For<IRepository<Waybill, long>>();
        waybillRepo.GetAsync(6002).Returns(waybill);

        var store = Substitute.For<IRecycleWaybillExtensionStore>();
        var attachmentService = Substitute.For<IAttachmentService>();
        attachmentService
            .CreateOrReplaceBillPhotoAsync(Arg.Any<WeighingListItemDto>(), Arg.Any<string>())
            .Throws(new InvalidOperationException("disk full"));

        var service = CreateService(waybillRepo, store, attachmentService);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.ConfirmAsync(6002, DateTime.Now, stream));

        await store.DidNotReceiveWithAnyArgs().UpsertReceivingTimeAsync(default, default);
    }
}
