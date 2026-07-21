using MaterialClient.Recycle.Models;
using MaterialClient.Recycle.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace MaterialClient.Recycle.Backgrounds;

/// <summary>
///     Recycle 上报轮询后台 Worker。
///     以 <see cref="RecycleSyncOptions.PollIntervalSeconds" /> 为周期触发 <see cref="RecycleDataSyncService" />。
/// </summary>
public sealed class RecyclePollingBackgroundService : AsyncPeriodicBackgroundWorkerBase
{
    public RecyclePollingBackgroundService(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<RecycleSyncOptions> options)
        : base(timer, serviceScopeFactory)
    {
        // ABP Timer.Period 单位为毫秒；至少 1 秒，避免配置为 0 导致空转。
        var periodSeconds = options.Value.PollIntervalSeconds <= 0 ? 5 : options.Value.PollIntervalSeconds;
        Timer.Period = periodSeconds * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        try
        {
            if (workerContext.CancellationToken.IsCancellationRequested)
            {
                Logger.LogInformation("Recycle 轮询：取消请求，跳过本轮。");
                return;
            }

            var syncService = workerContext.ServiceProvider.GetRequiredService<RecycleDataSyncService>();
            var uowManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

            using var uow = uowManager.Begin(requiresNew: true, isTransactional: false);
            await syncService.SyncOnceAsync(workerContext.CancellationToken);
            await uow.CompleteAsync(workerContext.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Recycle 轮询：本轮取消。");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Recycle 轮询：本轮扫描失败。");
        }
    }
}
