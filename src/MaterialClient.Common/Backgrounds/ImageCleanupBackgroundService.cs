using MaterialClient.Common.Configuration;
using MaterialClient.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace MaterialClient.Common.Backgrounds;

/// <summary>
///     Periodic local Camera / Lpr image retention cleanup worker.
/// </summary>
public sealed class ImageCleanupBackgroundService : AsyncPeriodicBackgroundWorkerBase
{
    private readonly ImageCleanupOptions _options;
    private bool _initialDelayDone;

    public ImageCleanupBackgroundService(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ImageCleanupOptions> options)
        : base(timer, serviceScopeFactory)
    {
        _options = options.Value;
        var intervalHours = Math.Max(1, _options.IntervalHours);
        Timer.Period = (int)TimeSpan.FromHours(intervalHours).TotalMilliseconds;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        if (!_options.Enabled)
        {
            Logger.LogDebug("Image cleanup worker skipped (Enabled=false)");
            return;
        }

        try
        {
            if (!_initialDelayDone)
            {
                _initialDelayDone = true;
                var delay = ComputeInitialDelay(_options.InitialDelayHours);
                if (delay > TimeSpan.Zero)
                {
                    Logger.LogInformation(
                        "Image cleanup waiting {Delay} (InitialDelayHours={Hours}) before first run",
                        delay, _options.InitialDelayHours);
                    await Task.Delay(delay, workerContext.CancellationToken);
                }
            }

            if (workerContext.CancellationToken.IsCancellationRequested)
                return;

            Logger.LogInformation("Starting local image cleanup");
            var cleanup = workerContext.ServiceProvider.GetRequiredService<ILocalImageCleanupService>();
            var result = await cleanup.CleanupAsync(workerContext.CancellationToken);
            Logger.LogInformation(
                "Local image cleanup completed: Deleted={Deleted}, Failed={Failed}",
                result.DeletedFiles, result.FailedDeletes);
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Image cleanup cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Image cleanup worker failed");
        }
    }

    /// <summary>
    ///     Relative delay from the first worker tick. Non-positive hours yield zero delay.
    /// </summary>
    internal static TimeSpan ComputeInitialDelay(int initialDelayHours)
    {
        if (initialDelayHours <= 0)
            return TimeSpan.Zero;

        return TimeSpan.FromHours(initialDelayHours);
    }
}
