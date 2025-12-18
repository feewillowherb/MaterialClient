using System;
using System.Threading.Tasks;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace MaterialClient.Backgrounds;

/// <summary>
/// 10 分钟轮询的后台任务，使用 ABP 的 AsyncPeriodicBackgroundWorkerBase。
/// 执行逻辑在独立的 UOW 中运行，便于数据库交互。
/// </summary>
public sealed class PollingBackgroundService : AsyncPeriodicBackgroundWorkerBase
{
    public PollingBackgroundService(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        // 设置定时器间隔为 10 分钟
        Timer.Period = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        Logger.LogInformation("开始执行轮询后台任务");

        try
        {
            // 检查是否请求取消
            if (workerContext.CancellationToken.IsCancellationRequested)
            {
                Logger.LogInformation("检测到取消请求，停止轮询任务");
                return;
            }

            await WithUow(VerifyAuthAsync, workerContext.ServiceProvider, workerContext.CancellationToken);

            if (workerContext.CancellationToken.IsCancellationRequested) return;
            await WithUow(SyncMaterialAsync, workerContext.ServiceProvider, workerContext.CancellationToken);

            if (workerContext.CancellationToken.IsCancellationRequested) return;
            await WithUow(SyncMaterialTypeAsync, workerContext.ServiceProvider, workerContext.CancellationToken);

            if (workerContext.CancellationToken.IsCancellationRequested) return;
            await WithUow(SyncProviderAsync, workerContext.ServiceProvider, workerContext.CancellationToken);
            
            if (workerContext.CancellationToken.IsCancellationRequested) return;
            await WithUow(PushWaybillAsync, workerContext.ServiceProvider, workerContext.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("轮询后台任务被取消");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "轮询后台任务执行异常");
        }
    }

    private async Task WithUow(Func<IServiceProvider, System.Threading.CancellationToken, Task> action, IServiceProvider serviceProvider, System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var uowManager = serviceProvider.GetRequiredService<IUnitOfWorkManager>();

        using var uow = uowManager.Begin(requiresNew: true, isTransactional: false);
        await action(serviceProvider, cancellationToken);
        await uow.CompleteAsync(cancellationToken);
    }

    private async Task SyncMaterialAsync(IServiceProvider serviceProvider, System.Threading.CancellationToken cancellationToken)
    {
        var service = serviceProvider.GetRequiredService<ISyncMaterialService>();

        Logger.LogInformation("开始同步物料数据...");
        await service.SyncMaterialAsync();
        Logger.LogInformation("物料数据同步完成");
    }

    private async Task SyncMaterialTypeAsync(IServiceProvider serviceProvider, System.Threading.CancellationToken cancellationToken)
    {
        var service = serviceProvider.GetRequiredService<ISyncMaterialService>();

        Logger.LogInformation("开始同步物料类型数据...");
        await service.SyncMaterialTypeAsync();
        Logger.LogInformation("物料类型数据同步完成");
    }

    private async Task SyncProviderAsync(IServiceProvider serviceProvider, System.Threading.CancellationToken cancellationToken)
    {
        var service = serviceProvider.GetRequiredService<ISyncMaterialService>();

        Logger.LogInformation("开始同步供应商数据...");
        await service.SyncProviderAsync();
        Logger.LogInformation("供应商数据同步完成");
    }

    private async Task VerifyAuthAsync(IServiceProvider serviceProvider, System.Threading.CancellationToken cancellationToken)
    {
        var licenseService = serviceProvider.GetRequiredService<ILicenseService>();

        Logger.LogInformation("开始验证许可证...");
        var isValid = await licenseService.IsLicenseValidAsync();
        if (!isValid)
        {
            Logger.LogWarning("许可证无效或已过期");
            // 这里可以添加更多处理逻辑，例如发送通知等
        }
        else
        {
            Logger.LogInformation("许可证验证通过");
        }
    }

    private async Task PushWaybillAsync(IServiceProvider serviceProvider, System.Threading.CancellationToken cancellationToken)
    {
        var service = serviceProvider.GetRequiredService<IWeighingMatchingService>();

        Logger.LogInformation("开始推送运单数据...");
        await service.PushWaybillAsync(cancellationToken);
        Logger.LogInformation("运单数据推送完成");
    }
}