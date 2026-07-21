using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Recycle.Api;
using MaterialClient.Recycle.Models;
using MaterialClient.Recycle.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Common.EntityFrameworkCore;

/// <summary>
///     <see cref="RecycleDataSyncService" /> 的 DB-backed 测试基座。
///     提供：构造同步服务（注入捕获用 <see cref="IRecycleDataApi" /> mock）、临时照片文件、
///     以及 Sending 完成态 Waybill 的种子（含 Material/Provider/RecycleWaybillExtension/附件）。
/// </summary>
public abstract class RecycleDataSyncTestBase : MaterialClientTestBase<RecycleDataSyncTestModule>
{
    /// <summary>最近一次 §2.2 SubmitTransportRecordAsync 提交的记录（测试断言用）。</summary>
    protected List<RecycleTransportRecord>? LastSubmittedTransportRecords { get; private set; }

    /// <summary>
    ///     在 <paramref name="tempDir" /> 下写入一个真实照片文件并返回绝对路径（供 <see cref="AttachmentFile.LocalPath" />）。
    ///     <see cref="MaterialClient.Common.Utils.PathManager.ToAbsolutePath" /> 对绝对路径原样返回，故可被同步服务读取。
    /// </summary>
    protected static string CreateTempPhotoFile(string tempDir, byte[] content)
    {
        Directory.CreateDirectory(tempDir);
        var path = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".jpg");
        File.WriteAllBytes(path, content);
        return path;
    }

    /// <summary>
    ///     构造 <see cref="RecycleDataSyncService" />：手工注入 EF-backed 仓储、IUnitOfWorkManager、
    ///     <see cref="IOptions{RecycleSyncOptions}" /> 与捕获用 <see cref="IRecycleDataApi" /> mock（Code=200），
    ///     并补齐 ABP <c>DomainService</c> 基类的 <c>LazyServiceProvider</c>（<c>Logger</c> 依赖）——
    ///     测试宿主未加载 Recycle 模块，该服务未做约定注册与属性注入，故手工构造。
    /// </summary>
    protected RecycleDataSyncService CreateSyncService()
    {
        LastSubmittedTransportRecords = null;

        var sp = ServiceProvider;
        var api = sp.GetRequiredService<IRecycleDataApi>();
        api.SubmitTransportRecordAsync(
                Arg.Do<List<RecycleTransportRecord>>(list => LastSubmittedTransportRecords = list),
                Arg.Any<CancellationToken>())
            .Returns(new RecycleApiResponse { Code = 200 });
        api.SubmitMaterialTransportRecordAsync(
                Arg.Any<List<RecycleMaterialTransportRecord>>(),
                Arg.Any<CancellationToken>())
            .Returns(new RecycleApiResponse { Code = 200 });

        var service = new RecycleDataSyncService(
            sp.GetRequiredService<IRepository<Waybill, long>>(),
            sp.GetRequiredService<IRepository<WaybillAttachment, int>>(),
            sp.GetRequiredService<IRepository<AttachmentFile, int>>(),
            sp.GetRequiredService<IRepository<WaybillMaterial, int>>(),
            sp.GetRequiredService<IRepository<Material, int>>(),
            sp.GetRequiredService<IRepository<Provider, int>>(),
            sp.GetRequiredService<IRepository<RecycleWaybillExtension, Guid>>(),
            api,
            sp.GetRequiredService<IOptions<RecycleSyncOptions>>(),
            sp.GetRequiredService<IUnitOfWorkManager>());

        service.LazyServiceProvider = new AbpLazyServiceProvider(sp);
        return service;
    }

    /// <summary>
    ///     在环境 UnitOfWork 中执行 <see cref="RecycleDataSyncService.SyncOnceAsync" />——
    ///     生产侧由 <c>RecyclePollingBackgroundService</c> 以 <c>uowManager.Begin</c> 包裹，照片构建查询（在
    ///     <c>SubmitSendingAsync</c> 自身 Begin 之前执行）依赖该环境 UoW。测试侧用 <see cref="WithUnitOfWorkAsync" /> 复现。
    /// </summary>
    protected Task RunSyncOnceInUnitOfWorkAsync(RecycleDataSyncService service)
        => WithUnitOfWorkAsync(() => service.SyncOnceAsync());

    /// <summary>
    ///     种子一条 Sending 完成态 Waybill（含 Material，可选 Provider/RecycleWaybillExtension/附件照片）。
    ///     附件按显式 <c>FileId</c> 关联（SQLite 接受显式 id），<see cref="WaybillAttachment.AttachmentFileId" /> 指向同 id。
    /// </summary>
    protected async Task SeedSendingCompletedWaybillAsync(
        long waybillId,
        IReadOnlyList<(int FileId, AttachType Type, string Path)> photos,
        bool seedExtension = false,
        bool seedProvider = false,
        string? providerAddress = null)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var matRepo = ServiceProvider.GetRequiredService<IRepository<Material, int>>();
            var provRepo = ServiceProvider.GetRequiredService<IRepository<Provider, int>>();
            var waybillRepo = ServiceProvider.GetRequiredService<IRepository<Waybill, long>>();
            var extRepo = ServiceProvider.GetRequiredService<IRepository<RecycleWaybillExtension, Guid>>();
            var afRepo = ServiceProvider.GetRequiredService<IRepository<AttachmentFile, int>>();
            var waRepo = ServiceProvider.GetRequiredService<IRepository<WaybillAttachment, int>>();

            await matRepo.InsertAsync(new Material(500, "成品灰土", 1) { WeighingMode = WeighingMode.Recycle });

            if (seedProvider)
            {
                await provRepo.InsertAsync(new Provider(600, 1, "回收运输公司") { Address = providerAddress });
            }

            await waybillRepo.InsertAsync(new Waybill(waybillId, $"fl-{waybillId}")
            {
                WeighingMode = WeighingMode.Recycle,
                OrderType = OrderTypeEnum.Completed,
                DeliveryType = DeliveryType.Sending,
                OrderGoodsWeight = 12.5m,
                MaterialId = 500,
                ProviderId = seedProvider ? 600 : null,
                IsPendingSync = true
            });

            if (seedExtension)
            {
                await extRepo.InsertAsync(new RecycleWaybillExtension(waybillId)
                {
                    UnitPrice = 120.0m,
                    SaleContractNo = "HT-2026-0001",
                    ReceivingTime = new DateTime(2026, 7, 9, 15, 20, 0)
                });
            }

            foreach (var (fileId, type, path) in photos)
            {
                await afRepo.InsertAsync(new AttachmentFile(fileId, $"{type}.jpg", path, type));
                await waRepo.InsertAsync(new WaybillAttachment(fileId, waybillId, fileId));
            }
        });
    }
}
