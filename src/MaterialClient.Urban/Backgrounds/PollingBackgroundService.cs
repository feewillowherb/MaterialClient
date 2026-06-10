using System;
using System.Threading;
using System.Threading.Tasks;
using MaterialClient.Common.Services.Urban;
using MaterialClient.Urban.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace MaterialClient.Urban.Backgrounds;

/// <summary>
///     Urban 称重记录 Pending 上云轮询（与主程序 PollingBackgroundService 同机制，独立实现）。
/// </summary>
public sealed class PollingBackgroundService : AsyncPeriodicBackgroundWorkerBase
{
    public PollingBackgroundService(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = configuration.GetValue("Urban:UploadPollingPeriodMs", 600_000);
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        Logger.LogInformation("Urban polling: starting weighing record upload scan");

        try
        {
            if (workerContext.CancellationToken.IsCancellationRequested)
            {
                Logger.LogInformation("Urban polling: cancellation requested before work");
                return;
            }

            var configuration = workerContext.ServiceProvider.GetRequiredService<IConfiguration>();
            var batchSize = configuration.GetValue("Urban:UploadBatchSize", 50);

            await WithUow(
                (serviceProvider, cancellationToken) =>
                    UploadPendingRecordsAsync(serviceProvider, batchSize, cancellationToken),
                workerContext.ServiceProvider,
                workerContext.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Urban polling: upload scan cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Urban polling: upload scan failed");
        }
    }

    private static async Task UploadPendingRecordsAsync(
        IServiceProvider serviceProvider,
        int batchSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var extensionService = serviceProvider.GetRequiredService<IUrbanWeighingExtensionService>();
        var uploadService = serviceProvider.GetRequiredService<IUrbanServerUploadService>();

        var pending = await extensionService.GetPendingForUploadAsync(batchSize);

        foreach (var extension in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // if (extension.IsAnomaly)
            // {
            //     continue;
            // }

            try
            {
                await uploadService.SubmitRecordAsync(extension.WeighingRecordId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<PollingBackgroundService>>();
                logger.LogWarning(
                    ex,
                    "Urban upload failed for record {RecordId} (will retry on next poll)",
                    extension.WeighingRecordId);
            }
        }
    }

    private async Task WithUow(
        Func<IServiceProvider, CancellationToken, Task> action,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var uowManager = serviceProvider.GetRequiredService<IUnitOfWorkManager>();

        using var uow = uowManager.Begin(requiresNew: true, isTransactional: false);
        await action(serviceProvider, cancellationToken);
        await uow.CompleteAsync(cancellationToken);
    }
}
