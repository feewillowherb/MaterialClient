using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Utils;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

/// <summary>
///     车牌缓存记录
/// </summary>
public record PlateNumberCacheRecord
{
    /// <summary>
    ///     识别次数
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    ///     最后更新时间
    /// </summary>
    public DateTime LastUpdateTime { get; init; }
}

/// <summary>
///     重量稳定性信息
/// </summary>
public record WeightStabilityInfo
{
    /// <summary>
    ///     当前重量（窗口内最新值）
    /// </summary>
    public decimal Weight { get; init; }

    /// <summary>
    ///     是否稳定
    /// </summary>
    public bool IsStable { get; init; }

    /// <summary>
    ///     稳定值（稳定时为平均值，否则为null）
    /// </summary>
    public decimal? StableWeight { get; init; }

    /// <summary>
    ///     最小值
    /// </summary>
    public decimal Min { get; init; }

    /// <summary>
    ///     最大值
    /// </summary>
    public decimal Max { get; init; }

    /// <summary>
    ///     范围
    /// </summary>
    public decimal Range { get; init; }
}

/// <summary>
///     有人值守称重服务接口
/// </summary>
public interface IAttendedWeighingService : IAsyncDisposable
{
    /// <summary>
    ///     获取当前收发料类型
    /// </summary>
    DeliveryType CurrentDeliveryType { get; }

    /// <summary>
    ///     启动监听
    /// </summary>
    Task StartAsync();

    /// <summary>
    ///     停止监听
    /// </summary>
    Task StopAsync();

    /// <summary>
    ///     获取当前状态
    /// </summary>
    AttendedWeighingStatus GetCurrentStatus();

    /// <summary>
    ///     接收车牌识别结果
    /// </summary>
    void OnPlateNumberRecognized(string plateNumber);

    /// <summary>
    ///     获取当前识别次数最大的车牌号
    /// </summary>
    string? GetMostFrequentPlateNumber();

    /// <summary>
    ///     设置收发料类型
    /// </summary>
    void SetDeliveryType(DeliveryType deliveryType);
}

/// <summary>
///     有人值守称重服务
///     监听地磅重量变化，管理称重状态，处理车牌识别缓存，并在适当时机进行抓拍和创建称重记录
/// </summary>
[AutoConstructor]
public partial class AttendedWeighingService : IAttendedWeighingService, ISingletonDependency
{
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;

    private readonly IHikvisionService _hikvisionService;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<AttendedWeighingService> _logger;

    private readonly ISettingsService _settingsService;

    // 统一状态管理（RxState 模式）
    private readonly BehaviorSubject<WeighingServiceState> _stateSubject = new(WeighingServiceState.Initial);

    // 外部 Action 输入流（用于接收外部事件）
    private readonly Subject<StateAction> _actionSubject = new();

    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IRepository<WeighingRecordAttachment, int> _weighingRecordAttachmentRepository;

    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;

    // 订阅管理
    private IDisposable? _stateSubscription;

    // 异步操作追踪（用于优雅关闭）
    private readonly ConcurrentBag<Task> _pendingOperations = new();
    private readonly object _operationsLock = new();

    // 异步操作流（用于错误处理和监控）
    private Subject<Func<Task>>? _asyncOperationsStream;
    private IDisposable? _asyncOperationsSubscription;


    /// <summary>
    ///     启动监听
    /// </summary>
    public async Task StartAsync()
    {
        // Load configuration from settings
        await LoadConfigurationAsync();

        if (_stateSubscription != null) return; // 已经启动

        // 重置状态
        var initialState = WeighingServiceState.Initial;
        var config = await GetConfigurationAsync();
        initialState = initialState with { Config = config };
        _stateSubject.OnNext(initialState);
        
        // 共享源流，避免多次订阅
        var sharedWeightSource = _truckScaleWeightService.WeightUpdates
            .Publish()
            .RefCount();

        // 创建各个流
        var weightStream = CreateWeightStream(sharedWeightSource, config);
        var stabilityStream = CreateStabilityStream(sharedWeightSource, config);
        
        // 创建状态流（使用 RxState 模式）
        var stateStream = CreateStateStream(weightStream, stabilityStream, initialState);

        // 订阅状态变化（包含错误处理和重试机制）
        _stateSubscription = SubscribeToStateChanges(stateStream);

        // 5. 创建异步操作处理流（用于错误处理和监控）
        var asyncOperationsStream = new Subject<Func<Task>>();
        
        _asyncOperationsSubscription = asyncOperationsStream
            .Select(operation => Observable.FromAsync(async () =>
            {
                var task = operation();
                lock (_operationsLock)
                {
                    _pendingOperations.Add(task);
                }
                
                try
                {
                    await task;
                    return (Success: true, Error: (Exception?)null);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in async operation");
                    return (Success: false, Error: (Exception?)ex);
                }
                finally
                {
                    lock (_operationsLock)
                    {
                        // 移除已完成的任务（优化：重建集合，只保留未完成的任务）
                        // 注意：由于 ConcurrentBag 不支持直接移除，我们通过重建来优化
                        var remainingTasks = _pendingOperations.Where(t => !t.IsCompleted).ToList();
                        if (remainingTasks.Count < _pendingOperations.Count)
                        {
                            // 有任务已完成，重建集合
                            _pendingOperations.Clear();
                            foreach (var remainingTask in remainingTasks)
                            {
                                _pendingOperations.Add(remainingTask);
                            }
                        }
                    }
                }
            }))
            .Merge(maxConcurrent: 5) // 最多5个并发操作，防止过载
            .Catch((Exception ex) =>
            {
                _logger?.LogError(ex, "Critical error in async operations stream");
                return Observable.Empty<(bool Success, Exception? Error)>();
            })
            .Retry(3) // 重试3次
            .Subscribe(
                result =>
                {
                    if (!result.Success)
                    {
                        _logger?.LogWarning("Async operation failed, may need manual intervention");
                        // 可以在这里添加失败重试队列或通知机制
                    }
                },
                error =>
                {
                    _logger?.LogError(error, "Fatal error in async operations stream");
                });

        // 保存异步操作流引用（用于后续添加操作）
        _asyncOperationsStream = asyncOperationsStream;

        _logger?.LogInformation("Started monitoring truck scale weight changes");

        await Task.CompletedTask;
    }

    /// <summary>
    ///     停止监听
    /// </summary>
    public async Task StopAsync()
    {
        // 停止接收新的事件
        _stateSubscription?.Dispose();
        _stateSubscription = null;
        
        // 停止接收新的 Action
        try
        {
            _actionSubject?.OnCompleted();
        }
        catch (InvalidOperationException)
        {
            // Stream already completed, ignore
        }
        
        // 停止接收新的异步操作
        try
        {
            _asyncOperationsStream?.OnCompleted();
        }
        catch (InvalidOperationException)
        {
            // Stream already completed, ignore
        }
        _asyncOperationsStream?.Dispose();
        _asyncOperationsStream = null;
        _asyncOperationsSubscription?.Dispose();
        _asyncOperationsSubscription = null;
        
        // 等待所有进行中的操作完成（优雅关闭）
        var pendingTasks = _pendingOperations.ToArray();
        if (pendingTasks.Length > 0)
        {
            _logger?.LogInformation(
                $"Waiting for {pendingTasks.Length} pending operations to complete...");
            
            try
            {
                // 设置超时，避免无限等待
                var timeout = TimeSpan.FromMinutes(5);
                var allTasksCompleted = Task.WhenAll(pendingTasks);
                var timeoutTask = Task.Delay(timeout);
                var completed = await Task.WhenAny(allTasksCompleted, timeoutTask);
                
                if (completed == allTasksCompleted)
                {
                    _logger?.LogInformation("All pending operations completed");
                }
                else
                {
                    _logger?.LogWarning(
                        $"Timeout waiting for operations to complete. {pendingTasks.Length} operations may still be running.");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while waiting for pending operations");
            }
        }
        
        _logger?.LogInformation("Stopped monitoring truck scale weight changes");

        await Task.CompletedTask;
    }

    /// <summary>
    ///     获取当前状态
    /// </summary>
    public AttendedWeighingStatus GetCurrentStatus()
    {
        return _stateSubject.Value.Status;
    }

    /// <summary>
    ///     获取当前收发料类型
    /// </summary>
    public DeliveryType CurrentDeliveryType => _stateSubject.Value.DeliveryType;

    /// <summary>
    ///     设置收发料类型
    /// </summary>
    public void SetDeliveryType(DeliveryType deliveryType)
    {
        var currentState = _stateSubject.Value;
        if (currentState.DeliveryType != deliveryType)
        {
            _actionSubject.OnNext(new SetDeliveryTypeAction(deliveryType));
            _logger?.LogInformation($"DeliveryType changed to {deliveryType}");
            
            // Send MessageBus notification
            var message = new DeliveryTypeChangedMessage(deliveryType);
            MessageBus.Current.SendMessage(message);
        }
    }

    /// <summary>
    ///     接收车牌识别结果
    /// </summary>
    public void OnPlateNumberRecognized(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber)) return;

        _actionSubject.OnNext(new PlateNumberRecognizedAction(plateNumber));
        
        // Notify observers of plate number update via MessageBus
        var mostFrequent = GetMostFrequentPlateNumber();
        var message = new PlateNumberChangedMessage(mostFrequent);
        MessageBus.Current.SendMessage(message);
    }

    /// <summary>
    ///     获取当前识别次数最大的车牌号
    /// </summary>
    public string? GetMostFrequentPlateNumber()
    {
        var cache = _stateSubject.Value.PlateNumberCache;
        if (cache.IsEmpty) return null;

        var mostFrequent = cache
            .OrderByDescending(kvp => kvp.Value.Count)
            .FirstOrDefault();

        return mostFrequent.Key;
    }

    /// <summary>
    ///     释放资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        
        // Safely complete and dispose internal subjects (used for state management)
        try
        {
            _stateSubject?.OnCompleted();
        }
        catch (InvalidOperationException)
        {
            // Subject already in error or completed state, ignore
        }
        finally
        {
            _stateSubject?.Dispose();
        }

        try
        {
            _actionSubject?.OnCompleted();
        }
        catch (InvalidOperationException)
        {
            // Subject already in error or completed state, ignore
        }
        finally
        {
            _actionSubject?.Dispose();
        }
    }

    /// <summary>
    ///     将异步操作加入处理队列（使用 Rx 流处理）
    /// </summary>
    private void EnqueueAsyncOperation(Func<Task> operation)
    {
        if (_asyncOperationsStream == null)
        {
            // 如果流未初始化，回退到 Task.Run（向后兼容）
            _ = Task.Run(async () =>
            {
                try
                {
                    await operation();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in async operation (fallback mode)");
                }
            });
            return;
        }

        try
        {
            _asyncOperationsStream.OnNext(operation);
        }
        catch (InvalidOperationException)
        {
            // 流已关闭，使用 Task.Run 作为后备
            _logger?.LogWarning("Async operations stream is closed, using fallback Task.Run");
            _ = Task.Run(async () =>
            {
                try
                {
                    await operation();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in async operation (fallback mode)");
                }
            });
        }
    }

    /// <summary>
    ///     创建重量流（更频繁，用于状态转换）
    /// </summary>
    private IObservable<decimal> CreateWeightStream(IObservable<decimal> sharedWeightSource, WeighingConfiguration config)
    {
        return sharedWeightSource
            .Buffer(TimeSpan.FromMilliseconds(config.StabilityCheckIntervalMs))
            .Where(buffer => buffer.Count > 0)
            .Select(buffer => buffer.Last())
            .DistinctUntilChanged() // 只在重量变化时发出
            .StartWith(0m);
    }

    /// <summary>
    ///     创建稳定性流（较慢，用于稳定性检查）
    /// </summary>
    private IObservable<WeightStabilityInfo> CreateStabilityStream(IObservable<decimal> sharedWeightSource, WeighingConfiguration config)
    {
        return sharedWeightSource
            .Buffer(TimeSpan.FromMilliseconds(config.StabilityWindowMs),
                TimeSpan.FromMilliseconds(config.StabilityCheckIntervalMs))
            .Select(buffer =>
            {
                if (buffer.Count > 0)
                {
                    var min = buffer.Min();
                    var max = buffer.Max();
                    var range = max - min;
                    var isStable = range <= config.WeightStabilityThreshold * 2;
                    var stableWeight = isStable ? (min + max) / 2 : (decimal?)null;

                    _logger?.LogDebug(
                        $"Weight stability: {isStable} (range: {range:F3} kg, min: {min:F3}, max: {max:F3}, stableWeight: {stableWeight:F3})");

                    return new WeightStabilityInfo
                    {
                        Weight = 0m, // Not used in stability stream
                        IsStable = isStable,
                        StableWeight = stableWeight,
                        Min = min,
                        Max = max,
                        Range = range
                    };
                }

                // No data, consider unstable
                return new WeightStabilityInfo
                {
                    Weight = 0m,
                    IsStable = false,
                    StableWeight = null,
                    Min = 0m,
                    Max = 0m,
                    Range = 0m
                };
            })
            .StartWith(new WeightStabilityInfo
            {
                Weight = 0m,
                IsStable = false,
                StableWeight = null,
                Min = 0m,
                Max = 0m,
                Range = 0m
            })
            .DistinctUntilChanged(info => info.IsStable) // Only emit when stability changes
            .Replay(1)
            .RefCount();
    }

    /// <summary>
    ///     创建状态流（使用 RxState 模式）
    /// </summary>
    private IObservable<WeighingServiceState> CreateStateStream(
        IObservable<decimal> weightStream,
        IObservable<WeightStabilityInfo> stabilityStream,
        WeighingServiceState initialState)
    {
        // 将外部事件转换为 Action
        var weightActions = weightStream.Select(w => (StateAction)new WeightUpdatedAction(w));
        var stabilityActions = stabilityStream.Select(s => (StateAction)new StabilityUpdatedAction(s));
        var deliveryTypeActions = _stateSubject
            .Skip(1) // 跳过初始值
            .Select(state => state.DeliveryType)
            .DistinctUntilChanged()
            .Select(dt => (StateAction)new SetDeliveryTypeAction(dt));
        var recordIdActions = _stateSubject
            .Skip(1) // 跳过初始值
            .Select(state => state.LastCreatedWeighingRecordId)
            .DistinctUntilChanged()
            .Select(id => (StateAction)new WeighingRecordCreatedAction(id));

        // 合并所有 Action 流（包括外部 Action）
        var actions = Observable.Merge(
            weightActions,
            stabilityActions,
            deliveryTypeActions,
            recordIdActions,
            _actionSubject.AsObservable()
        );

        // 使用 Scan 进行状态转换，同时跟踪前一个状态
        var stateWithPrevious = actions
            .Scan(
                (Previous: initialState, Current: initialState),
                (acc, action) => (Previous: acc.Current, Current: WeighingServiceStateReducer.ReduceState(acc.Current, action)))
            .StartWith((Previous: initialState, Current: initialState))
            .DistinctUntilChanged(pair => (pair.Current.Status, pair.Current.Weight, pair.Current.Stability.IsStable, pair.Current.LastCreatedWeighingRecordId)); // 只在关键状态变化时发出

        // 处理副作用（状态变化时的操作）
        return stateWithPrevious
            .Do(pair => ProcessStateTransition(pair.Previous, pair.Current))
            .Select(pair => pair.Current);
    }

    /// <summary>
    ///     订阅状态变化（包含错误处理和重试机制）
    /// </summary>
    private IDisposable SubscribeToStateChanges(IObservable<WeighingServiceState> stateStream)
    {
        return stateStream
            .Catch((Exception ex) =>
            {
                _logger?.LogError(ex, "Error in state stream, will retry in 5 seconds");
                // 延迟后重新订阅（通过返回空流触发重试）
                return Observable.Timer(TimeSpan.FromSeconds(5))
                    .SelectMany(_ => Observable.Empty<WeighingServiceState>());
            })
            .Retry(3) // 最多重试3次
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(
                state =>
                {
                    // 状态已通过 Subject 更新，这里只处理副作用
                    OnStateChanged(state);
                },
                error =>
                {
                    _logger?.LogError(error, "Fatal error in state stream subscription after retries");
                    // 可以考虑发送错误通知或进入安全模式
                });
    }

    /// <summary>
    ///     Load configuration from settings
    /// </summary>
    private async Task LoadConfigurationAsync()
    {
        try
        {
            var config = await GetConfigurationAsync();
            _logger?.LogInformation(
                $"Loaded configuration - MinWeightThreshold: {config.MinWeightThreshold}, WeightStabilityThreshold: {config.WeightStabilityThreshold}, StabilityWindowMs: {config.StabilityWindowMs}, StabilityCheckIntervalMs: {config.StabilityCheckIntervalMs}");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "Failed to load configuration, using default values");
        }
    }

    /// <summary>
    ///     获取配置
    /// </summary>
    private async Task<WeighingConfiguration> GetConfigurationAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            return settings.WeighingConfiguration;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load configuration, using default values");
            return new WeighingConfiguration();
        }
    }


    /// <summary>
    ///     状态变化处理（状态转换已在流中完成，这里只处理副作用）
    /// </summary>
    private void OnStateChanged(WeighingServiceState currentState)
    {
        // 更新状态 Subject（状态已在流中更新）
        _stateSubject.OnNext(currentState);

        // 发送状态变化通知
        MessageBus.Current.SendMessage(new StatusChangedMessage(currentState.Status));
    }

    /// <summary>
    ///     处理状态转换的副作用（纯副作用，不修改状态）
    /// </summary>
    private void ProcessStateTransition(
        WeighingServiceState previousState,
        WeighingServiceState currentState)
    {
        if (previousState.Status == currentState.Status)
            return;

        _logger?.LogInformation(
            $"Status changed {previousState.Status} -> {currentState.Status}, weight: {currentState.Weight:F3}t");

        // 处理状态转换的副作用
        switch (previousState.Status, currentState.Status)
        {
            case (AttendedWeighingStatus.OffScale, AttendedWeighingStatus.WaitingForStability):
                // 上磅：进入等待稳定状态
                _logger.LogInformation(
                    $"Entered WaitingForStability state (ascending), weight: {currentState.Weight:F3}t");
                break;

            case (AttendedWeighingStatus.WaitingForStability, AttendedWeighingStatus.OffScale):
                // 异常流程：未稳定就下磅
                _logger?.LogWarning(
                    $"Unstable weighing flow (abnormal departure), weight returned to {currentState.Weight:F3}t, triggered capture");
                
                // Capture all cameras and log (no need to save photos)
                EnqueueAsyncOperation(async () =>
                {
                    var photos = await CaptureAllCamerasAsync("UnstableWeighingFlow");
                    if (photos.Count == 0)
                        _logger?.LogWarning(
                            "Unstable weighing flow capture completed, but no photos were obtained");
                    else
                        _logger?.LogInformation(
                            $"Unstable weighing flow captured {photos.Count} photos");
                });

                // Try to rewrite plate number, then clear cache
                EnqueueAsyncOperation(async () => await ResetWeighingCycleAsync());
                break;

            case (AttendedWeighingStatus.WeightStabilized, AttendedWeighingStatus.WaitingForDeparture):
                // 正常流程：称重完成，进入等待下磅状态
                _logger?.LogInformation(
                    $"Entered WaitingForDeparture state (descending), weight: {currentState.Weight:F3}t");
                break;

            case (AttendedWeighingStatus.WeightStabilized, AttendedWeighingStatus.OffScale):
                // 异常流程：稳定后突然下磅（跳过WaitingForDeparture）
                _logger?.LogWarning(
                    $"Abnormal departure from WeightStabilized, weight returned to {currentState.Weight:F3}t");
                EnqueueAsyncOperation(async () => await ResetWeighingCycleAsync());
                break;

            case (AttendedWeighingStatus.WaitingForDeparture, AttendedWeighingStatus.OffScale):
                // 正常流程：正常下磅完成
                _logger?.LogInformation(
                    $"Normal flow completed (normal departure), entered OffScale state, weight: {currentState.Weight:F3}t");
                EnqueueAsyncOperation(async () => await ResetWeighingCycleAsync());
                break;
        }

        // 处理稳定性触发的操作
        if (currentState.Status == AttendedWeighingStatus.WeightStabilized &&
            currentState.Stability.IsStable &&
            currentState.LastCreatedWeighingRecordId == null) // 检查是否已经称重过（null表示未称重）
        {
            // Weight stabilized - use stable weight (average) if available
            var weightToUse = currentState.Stability.StableWeight ?? currentState.Weight;
            _logger?.LogInformation(
                $"Weight stabilized, stable weight: {weightToUse:F3}t");

            // When weight is stabilized, capture photos and create WeighingRecord
            EnqueueAsyncOperation(async () => await OnWeightStabilizedAsync(weightToUse));
        }
    }

    /// <summary>
    ///     重置称重周期（统一处理称重周期重置逻辑）
    /// </summary>
    private async Task ResetWeighingCycleAsync()
    {
        await TryReWritePlateNumberAsync();
        ClearPlateNumberCache();
        // Clear weighing record ID flag (reset for new cycle)
        _actionSubject.OnNext(new ResetWeighingCycleAction());
    }


    /// <summary>
    ///     重量已稳定时的处理
    /// </summary>
    private async Task OnWeightStabilizedAsync(decimal currentWeight)
    {
        try
        {
            // Capture all cameras
            var photoPaths = await CaptureAllCamerasAsync("WeightStabilized");

            // 创建WeighingRecord（传入照片路径）
            await CreateWeighingRecordAsync(currentWeight, photoPaths);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error occurred while processing weight stabilization");
        }
    }

    /// <summary>
    ///     抓拍所有相机
    /// </summary>
    /// <returns>成功抓拍的照片路径列表</returns>
    private async Task<List<string>> CaptureAllCamerasAsync(string reason)
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            var cameraConfigs = settings.CameraConfigs;

            if (cameraConfigs.Count == 0)
            {
                _logger?.LogWarning($"No cameras configured, cannot capture ({reason})");
                return new List<string>();
            }

            // 转换为 BatchCaptureRequest
            var requests = new List<BatchCaptureRequest>();
            var now = DateTime.Now;
            var basePath = AttachmentPathUtils.GetLocalStoragePath(AttachType.EntryPhoto, now);

            foreach (var cameraConfig in cameraConfigs)
            {
                var request = BatchCaptureRequest.FromCameraConfig(cameraConfig, basePath, _logger);
                if (request != null)
                {
                    requests.Add(request);
                }
            }

            if (requests.Count == 0)
            {
                _logger?.LogWarning(
                    $"No valid camera configurations, cannot capture ({reason})");
                return new List<string>();
            }

            _logger?.LogInformation(
                $"Starting capture for {requests.Count} cameras ({reason})");

            var results = await _hikvisionService.CaptureJpegFromStreamBatchAsync(requests);

            var successCount = results.Count(r => r.Success);
            var failCount = results.Count - successCount;

            _logger?.LogInformation(
                $"Capture completed, success: {successCount}, failed: {failCount} ({reason})");

            // Log detailed failure information
            foreach (var result in results.Where(r => !r.Success))
                _logger?.LogWarning(
                    $"Capture failed - Device: {result.Request.DeviceKey}, Channel: {result.Request.Channel}, Error: {result.ErrorMessage}");

            // Return list of successfully captured photo paths
            var photoPaths = results.Where(r => r.Success && File.Exists(r.Request.SaveFullPath))
                .Select(r => r.Request.SaveFullPath)
                .ToList();

            // Log if photo list is empty
            if (photoPaths.Count == 0)
                _logger?.LogWarning(
                    $"Capture completed, but no photos were successfully obtained ({reason})");

            return photoPaths;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Error occurred while capturing all cameras ({reason})");
            _logger?.LogWarning($"Capture exception, returning empty photo list ({reason})");
            return new List<string>();
        }
    }

    /// <summary>
    ///     创建称重记录
    /// </summary>
    private async Task CreateWeighingRecordAsync(decimal weight, List<string> photoPaths)
    {
        try
        {
            var plateNumber = GetMostFrequentPlateNumber();

            using var uow = _unitOfWorkManager.Begin();

            // Create weighing record with current delivery type
            var currentState = _stateSubject.Value;
            var weighingRecord = new WeighingRecord(weight, plateNumber);
            weighingRecord.DeliveryType = currentState.DeliveryType;
            await _weighingRecordRepository.InsertAsync(weighingRecord);
            await uow.CompleteAsync();

            _logger?.LogInformation(
                $"Created weighing record successfully, ID: {weighingRecord.Id}, Weight: {weight}t, PlateNumber: {plateNumber ?? "None"}, DeliveryType: {currentState.DeliveryType}");

            // 保存最近创建的称重记录ID，用于后续重写车牌号（通过 Action 传递）
            _actionSubject.OnNext(new WeighingRecordCreatedAction(weighingRecord.Id));

            // Notify observers that a new weighing record was created via MessageBus
            var message = new WeighingRecordCreatedMessage(weighingRecord.Id);
            MessageBus.Current.SendMessage(message);

            // Publish TryMatchEvent for automatic matching

            // Save captured photos to WeighingRecordAttachment
            if (photoPaths.Count > 0)
                await SaveCapturePhotosAsync(weighingRecord.Id, photoPaths);
            else
                _logger?.LogWarning(
                    $"Weighing record {weighingRecord.Id} has no associated photos");
            
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error occurred while creating weighing record");
        }
    }

    /// <summary>
    ///     保存抓拍的照片
    /// </summary>
    private async Task SaveCapturePhotosAsync(long weighingRecordId, List<string> photoPaths)
    {
        try
        {
            if (photoPaths.Count == 0) return;

            using var uow = _unitOfWorkManager.Begin();

            foreach (var photoPath in photoPaths)
                try
                {
                    if (!File.Exists(photoPath))
                    {
                        _logger?.LogWarning($"Photo file does not exist: {photoPath}");
                        continue;
                    }

                    var fileName = Path.GetFileName(photoPath);
                    var attachmentFile = new AttachmentFile(fileName, photoPath, AttachType.UnmatchedEntryPhoto);

                    await _attachmentFileRepository.InsertAsync(attachmentFile, true);

                    var weighingRecordAttachment = new WeighingRecordAttachment(weighingRecordId, attachmentFile.Id);
                    await _weighingRecordAttachmentRepository.InsertAsync(weighingRecordAttachment, true);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, $"Failed to save photo: {photoPath}");
                }

            await uow.CompleteAsync();
            _logger?.LogInformation(
                $"Saved {photoPaths.Count} photos to weighing record {weighingRecordId}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error occurred while saving captured photos");
        }
    }

    /// <summary>
    ///     尝试重写称重记录的车牌号
    ///     在清空车牌缓存前调用，用最频繁识别的车牌号更新最近创建的称重记录
    /// </summary>
    private async Task TryReWritePlateNumberAsync()
    {
        // Get latest record ID directly from state
        var recordId = _stateSubject.Value.LastCreatedWeighingRecordId;

        try
        {
            if (recordId == null)
            {
                _logger?.LogDebug("No recent weighing record to rewrite plate number");
                return;
            }

            var plateNumber = GetMostFrequentPlateNumber();
            if (string.IsNullOrWhiteSpace(plateNumber))
            {
                _logger?.LogDebug("No plate number to rewrite");
                return;
            }

            using var uow = _unitOfWorkManager.Begin();
            var weighingRecord = await _weighingRecordRepository.GetAsync(recordId.Value);

            if (weighingRecord.PlateNumber != plateNumber)
            {
                var oldPlateNumber = weighingRecord.PlateNumber;
                weighingRecord.PlateNumber = plateNumber;
                await _weighingRecordRepository.UpdateAsync(weighingRecord);
                await uow.CompleteAsync();

                _logger?.LogInformation(
                    $"Rewrote plate number for weighing record {weighingRecord.Id}, from '{oldPlateNumber ?? "None"}' to '{plateNumber}'");

                await _localEventBus.PublishAsync(new TryMatchEvent(weighingRecord.Id));

                // 通过 ReactiveUI MessageBus 发送更新车牌号消息
                var updateMessage = new UpdatePlateNumberMessage(weighingRecord.Id, plateNumber);
                MessageBus.Current.SendMessage(updateMessage);

                _logger?.LogInformation(
                    " Sent UpdatePlateNumberMessage via MessageBus for WeighingRecordId {RecordId}, PlateNumber {PlateNumber}",
                    weighingRecord.Id, plateNumber);
            }
            else
            {
                await uow.CompleteAsync();
                _logger?.LogDebug(
                    $"Plate number unchanged for weighing record {recordId.Value}");
                await _localEventBus.PublishAsync(new TryMatchEvent(weighingRecord.Id));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error occurred while rewriting plate number");
        }
    }

    /// <summary>
    ///     清空车牌缓存
    /// </summary>
    private void ClearPlateNumberCache()
    {
        // 车牌缓存清空通过 ResetWeighingCycleAction 处理
        _logger?.LogDebug("Cleared plate number cache");

        // Notify observers that plate number is cleared via MessageBus
        var message = new PlateNumberChangedMessage(null);
        MessageBus.Current.SendMessage(message);
    }
}