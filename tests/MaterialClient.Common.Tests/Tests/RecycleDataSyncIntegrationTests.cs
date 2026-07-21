using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     集成测试：Recycle 收货 → 后台同步端到端。
///     收货（RecycleReceivingService）写入 RecycleWaybillExtension/TicketPhoto 后，<see cref="RecycleDataSyncService.SyncOnceAsync" />
///     生成的 §2.2 payload SHALL 含全部新字段（unitPrice/saleContractNo/receivingTime/receivingProof/consigneeAddress）
///     与进场+出场聚合照片。本测试直接 seed 收货产物（Extension + TicketPhoto），验证同步读取与透传。
/// </summary>
public class RecycleDataSyncIntegrationTests : RecycleDataSyncTestBase
{
    [Fact]
    public async Task SubmitSending_Payload_Contains_All_New_Fields_And_OrderedPhotos()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mc-recycle-sync-" + Guid.NewGuid());
        try
        {
            var entryPath = CreateTempPhotoFile(tempDir, new byte[] { 1 });   // AQ==
            var exitPath = CreateTempPhotoFile(tempDir, new byte[] { 2 });    // Ag==
            var ticketPath = CreateTempPhotoFile(tempDir, new byte[] { 3 });  // Aw==

            // 收货已完成：Provider.Address、RecycleWaybillExtension（单价/合同号/收货时间）、TicketPhoto 均就绪。
            await SeedSendingCompletedWaybillAsync(
                7201,
                new[]
                {
                    (1001, AttachType.EntryPhoto, entryPath),
                    (1002, AttachType.ExitPhoto, exitPath),
                    (1003, AttachType.TicketPhoto, ticketPath)
                },
                seedExtension: true,
                seedProvider: true,
                providerAddress: "杭州市西湖区某路 1 号");

            var svc = CreateSyncService();
            await RunSyncOnceInUnitOfWorkAsync(svc);

            LastSubmittedTransportRecords.ShouldNotBeNull();
            var payload = LastSubmittedTransportRecords![0];

            // 进场+出场聚合（进场在前），收货照片透传。
            payload.OutPhotos.ShouldBe("AQ==,Ag==");
            payload.ReceivingProof.ShouldBe("Aw==");

            // RecycleWaybillExtension 透传字段。
            payload.UnitPrice.ShouldBe(120.0m);
            payload.SaleContractNo.ShouldBe("HT-2026-0001");
            payload.ReceivingTime.ShouldBe("2026-07-09 15:20:00");

            // Provider.Address → consigneeAddress。
            payload.ConsigneeAddress.ShouldBe("杭州市西湖区某路 1 号");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SubmitSending_Nulls_Optional_Fields_When_No_Extension_Or_Address_Or_Ticket()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mc-recycle-sync-" + Guid.NewGuid());
        try
        {
            var entryPath = CreateTempPhotoFile(tempDir, new byte[] { 1 });
            var exitPath = CreateTempPhotoFile(tempDir, new byte[] { 2 });

            // 未收货：无 Extension、无 Provider、无 TicketPhoto。
            await SeedSendingCompletedWaybillAsync(7202, new[]
            {
                (1001, AttachType.EntryPhoto, entryPath),
                (1002, AttachType.ExitPhoto, exitPath)
            });

            var svc = CreateSyncService();
            await RunSyncOnceInUnitOfWorkAsync(svc);

            LastSubmittedTransportRecords.ShouldNotBeNull();
            var payload = LastSubmittedTransportRecords![0];

            // 进场/出场照片仍聚合（上报流程不因可选字段缺失中断）。
            payload.OutPhotos.ShouldBe("AQ==,Ag==");

            payload.UnitPrice.ShouldBeNull();
            payload.SaleContractNo.ShouldBeNull();
            payload.ReceivingTime.ShouldBeNull();
            payload.ReceivingProof.ShouldBeNull();
            payload.ConsigneeAddress.ShouldBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
