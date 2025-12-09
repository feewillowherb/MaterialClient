using System;
using System.Threading;
using System.Threading.Tasks;
using MaterialClient.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Volo.Abp.Uow;

namespace MaterialClient.Backgrounds;

/// <summary>
/// 10 分钟轮询的后台任务骨架，实际业务逻辑留空 //TODO。
/// 执行逻辑在独立的 UOW 中运行，便于数据库交互。
/// </summary>
public sealed class PollingBackgroundService : IHostedService, IAsyncDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<PollingBackgroundService> _logger;
    private CancellationTokenSource? _stoppingCts;
    private Task? _executingTask;
    private PeriodicTimer? _timer;

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    public PollingBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<PollingBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _timer = new PeriodicTimer(Interval);
        _executingTask = Task.Run(() => RunAsync(_stoppingCts.Token), cancellationToken);
        _logger.LogInformation("TODO 轮询后台任务已启动，间隔 {Interval}", Interval);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TODO 轮询后台任务正在停止");

        if (_stoppingCts is { IsCancellationRequested: false })
        {
            await _stoppingCts.CancelAsync();
        }

        if (_executingTask != null)
        {
            await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }

        _timer?.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_timer == null)
        {
            return;
        }

        try
        {
            while (await _timer.WaitForNextTickAsync(cancellationToken))
            {
                await ExecuteWithUowAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止，忽略
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TODO 轮询后台任务执行异常");
        }
    }

    private async Task ExecuteWithUowAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        using var uow = uowManager.Begin(requiresNew: true, isTransactional: false);
        await SyncMaterialAsync(cancellationToken);
        await uow.CompleteAsync(cancellationToken);
    }

    private async Task SyncMaterialAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        var service = scope.ServiceProvider.GetRequiredService<ISyncMaterialService>();

        _logger.LogInformation("Starting SyncMaterial...");
        await service.SyncMaterialAsync();
        _logger.LogInformation("SyncMaterial Done");
    }

    public async ValueTask DisposeAsync()
    {
        _timer?.Dispose();
        if (_stoppingCts is { IsCancellationRequested: false })
        {
            await _stoppingCts.CancelAsync();
        }

        if (_executingTask != null)
        {
            try
            {
                await _executingTask;
            }
            catch (OperationCanceledException)
            {
                // 忽略停止时的取消异常
            }
        }

        _stoppingCts?.Dispose();
    }
}