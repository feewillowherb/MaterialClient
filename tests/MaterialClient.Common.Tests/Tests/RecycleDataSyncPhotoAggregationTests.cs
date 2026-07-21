using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     单测（DB-backed，经 <see cref="RecycleDataSyncService.SyncOnceAsync" /> 公开入口）：
///     §2.2 outPhotos 进场+出场聚合顺序（进场在前、出场在后）、缺失附件文件跳过、
///     ReceivingProof 无收货照片时返回 null。
///     验证 <c>BuildExitPhotosBase64Async</c>/<c>BuildReceivingProofBase64Async</c> 的聚合与容错语义。
/// </summary>
public class RecycleDataSyncPhotoAggregationTests : RecycleDataSyncTestBase
{
    [Fact]
    public async Task OutPhotos_Aggregates_Entry_First_Then_Exit()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mc-recycle-photo-" + Guid.NewGuid());
        try
        {
            var entryPath = CreateTempPhotoFile(tempDir, new byte[] { 1 }); // → AQ==
            var exitPath = CreateTempPhotoFile(tempDir, new byte[] { 2 });  // → Ag==

            await SeedSendingCompletedWaybillAsync(7101, new[]
            {
                (1001, AttachType.EntryPhoto, entryPath),
                (1002, AttachType.ExitPhoto, exitPath)
            });

            var svc = CreateSyncService();
            await RunSyncOnceInUnitOfWorkAsync(svc);

            LastSubmittedTransportRecords.ShouldNotBeNull();
            // 进场在前、出场在后，英文逗号分隔、无空格。
            LastSubmittedTransportRecords![0].OutPhotos.ShouldBe("AQ==,Ag==");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task OutPhotos_Skips_Photo_File_Missing_On_Disk()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mc-recycle-photo-" + Guid.NewGuid());
        try
        {
            var entryPath = CreateTempPhotoFile(tempDir, new byte[] { 1 }); // 存在 → AQ==
            var exitPath = Path.Combine(tempDir, "missing-exit.jpg");       // 磁盘缺失，应跳过

            await SeedSendingCompletedWaybillAsync(7102, new[]
            {
                (1001, AttachType.EntryPhoto, entryPath),
                (1002, AttachType.ExitPhoto, exitPath)
            });

            var svc = CreateSyncService();
            await RunSyncOnceInUnitOfWorkAsync(svc);

            LastSubmittedTransportRecords.ShouldNotBeNull();
            // 出场侧文件缺失被跳过，仅保留进场侧。
            LastSubmittedTransportRecords![0].OutPhotos.ShouldBe("AQ==");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ReceivingProof_Is_Null_When_No_Ticket_Photo()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mc-recycle-photo-" + Guid.NewGuid());
        try
        {
            var entryPath = CreateTempPhotoFile(tempDir, new byte[] { 1 });
            var exitPath = CreateTempPhotoFile(tempDir, new byte[] { 2 });

            // 有进场/出场照片，但无 TicketPhoto 收货照片。
            await SeedSendingCompletedWaybillAsync(7103, new[]
            {
                (1001, AttachType.EntryPhoto, entryPath),
                (1002, AttachType.ExitPhoto, exitPath)
            });

            var svc = CreateSyncService();
            await RunSyncOnceInUnitOfWorkAsync(svc);

            LastSubmittedTransportRecords.ShouldNotBeNull();
            // 无收货照片附件 → ReceivingProof 为 null（记日志但不中断上报）。
            LastSubmittedTransportRecords![0].ReceivingProof.ShouldBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
