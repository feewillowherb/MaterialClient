using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Extensions;
using MaterialClient.Common.Providers;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Services.LprAllInOne;
using MaterialClient.Common.Utils;
using Microsoft.Extensions.Configuration;
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

    /// <summary>
    ///     车牌颜色类型（用于优先级判断）
    /// </summary>
    public LprAllInOneColorType? ColorType { get; init; }
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

    private readonly IConfiguration _configuration;
    private readonly IHikvisionService _hikvisionService;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<AttendedWeighingService> _logger;

    private readonly ILprAllInOneService? _lprAllInOneService;
    private readonly ISettingsService _settingsService;
    private readonly ISoundDeviceService? _soundDeviceService;
    private readonly RecommendPlateNumberService _recommendPlateNumberService;

    // Rx Subject for status updates - using BehaviorSubject to maintain current state (internal use only)
    private readonly BehaviorSubject<AttendedWeighingStatus> _statusSubject = new(AttendedWeighingStatus.OffScale);

    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IRepository<WeighingRecordAttachment, int> _weighingRecordAttachmentRepository;

    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;

    // Delivery type management using BehaviorSubject (internal use only)
    private readonly BehaviorSubject<DeliveryType> _deliveryTypeSubject = new(DeliveryType.Receiving);

    // Last created weighing record ID stream (also used as flag: null = not weighed, >0 = weighed)
    private readonly BehaviorSubject<long?> _lastCreatedWeighingRecordIdSubject = new(null);

    // Configuration fields
    private decimal _minWeightThreshold;
    private decimal _weightStabilityThreshold;
    private int _stabilityWindowMs;
    private int _stabilityCheckIntervalMs;

    // Plate number cache (field-level management)
    private readonly ConcurrentDictionary<string, PlateNumberCacheRecord> _plateNumberCache = new();

    // Plate color priority config (initialized once at startup)
    private bool _plateColorFilterInitialized;
    private HashSet<LprAllInOneColorType> _lowPriorityPlateColors = new();

    // 订阅管理
    private IDisposable? _stateSubscription;
    private IDisposable? _licensePlateSubscription; // MessageBus 订阅

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

        // 订阅 MessageBus 车牌识别消息(统一事件传递)
        if (_licensePlateSubscription == null)
        {
            _licensePlateSubscription = MessageBus.Current
                .Listen<LicensePlateRecognizedMessage>()
                .Subscribe(msg =>
                {
                    try
                    {
                        _logger?.LogInformation(
                            "收到 LPR 事件: {Plate} 来自 {Device} (类型: {DeviceType})",
                            msg.PlateNumber, msg.DeviceName, msg.DeviceType);

                        // 调用现有处理逻辑
                        OnPlateNumberRecognized(msg.PlateNumber, msg.ColorType);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex,
                            "处理 LPR 消息失败: {Plate}",
                            msg.PlateNumber);
                    }
                });

            _logger?.LogInformation("已订阅 LicensePlateRecognizedMessage (MessageBus)");
        }

        // Load configuration into fields
        var config = await GetConfigurationAsync();
        _minWeightThreshold = config.MinWeightThreshold;
        _weightStabilityThreshold = config.WeightStabilityThreshold;
        _stabilityWindowMs = config.StabilityWindowMs;
        _stabilityCheckIntervalMs = config.StabilityCheckIntervalMs;

        // Load plate color priority config from appsettings.json (initialize once)
        if (!_plateColorFilterInitialized)
        {
            var lowPriorityColors = _configuration.GetSection("LowPriorityPlateColors").Get<LprAllInOneColorType[]>();
            if (lowPriorityColors == null || lowPriorityColors.Length == 0)
            {
                _lowPriorityPlateColors = new HashSet<LprAllInOneColorType>();
            }
            else
            {
                _lowPriorityPlateColors = lowPriorityColors
                    .Select(v => (LprAllInOneColorType)v)
                    .ToHashSet();
            }

            _plateColorFilterInitialized = true;
            _logger?.LogInformation("Loaded low-priority plate colors from appsettings: {Colors}",
                _lowPriorityPlateColors.Count == 0 ? "none" : string.Join(", ", _lowPriorityPlateColors));
        }

        // 共享源流，避免多次订阅，只保留最近5秒的数据（背压保护）
        var sharedWeightSource = _truckScaleWeightService.WeightUpdates
            .Publish()
            .RefCount();

        // 创建各个流
        var weightStream = CreateWeightStream(sharedWeightSource, config);
        var stabilityStream = CreateStabilityStream(sharedWeightSource, config);

        // 创建状态流
        var statusStream = CreateStatusStream(weightStream, stabilityStream);

        // 订阅状态变化（包含错误处理和重试机制）
        // 需要组合状态流、重量流和稳定性流来调用 OnWeightAndStatusChanged
        var combinedStream = statusStream
            .CombineLatest(
                weightStream,
                stabilityStream,
                (status, weight, stability) => (Status: status, Weight: weight, Stability: stability))
            .DistinctUntilChanged(t => t.Status);

        _stateSubscription = SubscribeToStatusChanges(combinedStream);

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
                // 注意：任务完成后不立即从 _pendingOperations 移除
                // 原因：ConcurrentBag 不支持直接移除，频繁重建集合性能差
                // 清理逻辑：在 StopAsync 中统一清理，或通过定期清理机制
                // 这样可以避免竞态条件，提高性能
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
                error => { _logger?.LogError(error, "Fatal error in async operations stream"); });

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
        // 只等待未完成的任务，避免等待已完成的任务导致性能问题
        var pendingTasks = _pendingOperations.Where(t => !t.IsCompleted).ToArray();
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

        // 清理已完成的任务，释放内存
        // 注意：由于 ConcurrentBag 不支持直接移除，我们通过重建来清理
        lock (_operationsLock)
        {
            var remainingTasks = _pendingOperations.Where(t => !t.IsCompleted).ToList();
            _pendingOperations.Clear();
            foreach (var remainingTask in remainingTasks)
            {
                _pendingOperations.Add(remainingTask);
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
        return _statusSubject.Value;
    }

    /// <summary>
    ///     获取当前收发料类型
    /// </summary>
    public DeliveryType CurrentDeliveryType => _deliveryTypeSubject.Value;

    /// <summary>
    ///     设置收发料类型
    /// </summary>
    public void SetDeliveryType(DeliveryType deliveryType)
    {
        if (_deliveryTypeSubject.Value != deliveryType)
        {
            _deliveryTypeSubject.OnNext(deliveryType);
            _logger?.LogInformation($"DeliveryType changed to {deliveryType}");

            // Send MessageBus notification
            var message = new DeliveryTypeChangedMessage(deliveryType);
            MessageBus.Current.SendMessage(message);
        }
    }

    /// <summary>
    ///     接收车牌识别结果(通过 MessageBus 订阅调用)
    /// </summary>
    private void OnPlateNumberRecognized(string plateNumber, LprAllInOneColorType? colorType = null)
    {
        if (string.IsNullOrWhiteSpace(plateNumber)) return;

        // Log low-priority plate colors (but don't reject them)
        if (colorType.HasValue && _lowPriorityPlateColors.Contains(colorType.Value))
        {
            _logger?.LogInformation("检测到低优先级车牌颜色: Plate={Plate}, Color={Color}",
                plateNumber, colorType.Value);
        }

        // 过滤掉"挂"字（仅处理简体"挂"）
        var filteredPlateNumber = PlateNumberValidator.FilterHangingCharacter(plateNumber, _logger);

        // 如果过滤后为空，则不处理
        if (string.IsNullOrWhiteSpace(filteredPlateNumber)) return;

        // 获取推荐的车牌号
        var recommendedPlateNumber = _recommendPlateNumberService.GetRecommendPlateNumber(filteredPlateNumber);

        // 如果推荐的车牌号与原始不同，记录日志
        if (recommendedPlateNumber != filteredPlateNumber)
        {
            _logger?.LogInformation(
                "车牌号推荐匹配: 原始={OriginalPlate}, 推荐={RecommendedPlate}",
                filteredPlateNumber,
                recommendedPlateNumber);
        }

        // 使用推荐的车牌号继续后续处理
        var finalPlateNumber = recommendedPlateNumber;

        // 只在车辆上磅期间缓存车牌号（OffScale 状态下不缓存）
        var currentStatus = _statusSubject.Value;
        if (currentStatus == AttendedWeighingStatus.OffScale)
        {
            return;
        }

        // 更新车牌缓存（使用推荐的车牌号，并存储颜色信息）
        _plateNumberCache.AddOrUpdate(
            finalPlateNumber,
            new PlateNumberCacheRecord { Count = 1, LastUpdateTime = DateTime.UtcNow, ColorType = colorType },
            (key, oldValue) => new PlateNumberCacheRecord
                { Count = oldValue.Count + 1, LastUpdateTime = DateTime.UtcNow, ColorType = colorType ?? oldValue.ColorType });

        // 获取最频繁的车牌号并发送通知
        var mostFrequent = GetMostFrequentPlateNumber();
        var message = new PlateNumberChangedMessage(mostFrequent);
        MessageBus.Current.SendMessage(message);
    }

    /// <summary>
    ///     获取当前识别次数最大的车牌号（优先选择高优先级车牌）
    /// </summary>
    public string? GetMostFrequentPlateNumber()
    {
        if (_plateNumberCache.IsEmpty) return null;

        // Separate high-priority and low-priority plates
        var highPriorityPlates = _plateNumberCache
            .Where(kvp => !kvp.Value.ColorType.HasValue || !_lowPriorityPlateColors.Contains(kvp.Value.ColorType.Value))
            .ToList();

        // If we have high-priority plates, select from them
        if (highPriorityPlates.Count > 0)
        {
            var mostFrequent = highPriorityPlates
                .OrderByDescending(kvp => kvp.Value.Count)
                .First();
            return mostFrequent.Key;
        }

        // Fall back to low-priority plates if no high-priority plates exist
        var lowPriorityMostFrequent = _plateNumberCache
            .OrderByDescending(kvp => kvp.Value.Count)
            .First();

        _logger?.LogInformation("使用低优先级车牌（无高优先级车牌可用）: Plate={Plate}, Color={Color}",
            lowPriorityMostFrequent.Key, lowPriorityMostFrequent.Value.ColorType);

        return lowPriorityMostFrequent.Key;
    }

    /// <summary>
    ///     释放资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        // 释放 MessageBus 订阅,防止内存泄漏
        try
        {
            _licensePlateSubscription?.Dispose();
            _licensePlateSubscription = null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "释放 MessageBus 订阅时发生异常");
        }

        // Safely complete and dispose internal subjects (used for state management)
        try
        {
            _statusSubject?.OnCompleted();
        }
        catch (InvalidOperationException)
        {
            // Subject already in error or completed state, ignore
        }
        finally
        {
            _statusSubject?.Dispose();
        }

        try
        {
            _deliveryTypeSubject?.OnCompleted();
        }
        catch (InvalidOperationException)
        {
            // Subject already in error or completed state, ignore
        }
        finally
        {
            _deliveryTypeSubject?.Dispose();
        }

        try
        {
            _lastCreatedWeighingRecordIdSubject?.OnCompleted();
        }
        catch (InvalidOperationException)
        {
            // Subject already in error or completed state, ignore
        }
        finally
        {
            _lastCreatedWeighingRecordIdSubject?.Dispose();
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
    private IObservable<decimal> CreateWeightStream(IObservable<decimal> sharedWeightSource,
        WeighingConfiguration config)
    {
        return sharedWeightSource
            .Buffer(TimeSpan.FromMilliseconds(config.StabilityCheckIntervalMs))
            .Where(buffer => buffer.Count > 0)
            .Select(buffer => buffer.Last())
            .StartWith(0m);
    }

    /// <summary>
    ///     创建稳定性流（较慢，用于稳定性检查）
    /// </summary>
    private IObservable<WeightStabilityInfo> CreateStabilityStream(IObservable<decimal> sharedWeightSource,
        WeighingConfiguration config)
    {
        // 计算最小数据点数量要求：至少需要覆盖窗口时间的 50% 以上
        // 例如：窗口 3000ms，检查间隔 200ms，期望至少 7-8 个数据点
        var minDataPointsRequired =
            Math.Max(8, (int)(config.StabilityWindowMs / config.StabilityCheckIntervalMs * 0.5));

        return sharedWeightSource
            .Buffer(TimeSpan.FromMilliseconds(config.StabilityWindowMs),
                TimeSpan.FromMilliseconds(config.StabilityCheckIntervalMs))
            .Select(buffer =>
            {
                if (buffer.Count > 0)
                {
                    // 关键修复：只统计大于MinWeightThreshold的数据点（有效称重数据）
                    var validDataPoints = buffer.Where(w => w > config.MinWeightThreshold).ToList();

                    if (validDataPoints.Count == 0)
                    {
                        // 没有有效数据点，判定为不稳定
                        return new WeightStabilityInfo
                        {
                            Weight = 0m,
                            IsStable = false,
                            StableWeight = null,
                            Min = 0,
                            Max = 0,
                            Range = 0
                        };
                    }

                    // 只在有效数据点上计算稳定性
                    var min = validDataPoints.Min();
                    var max = validDataPoints.Max();
                    var range = max - min;

                    // 关键修复：需要同时满足两个条件才判定为稳定
                    // 1. range 满足阈值要求
                    // 2. 窗口内有足够的大于MinWeightThreshold的数据点（防止上磅瞬间就判定为稳定）
                    var rangeStable = range <= config.WeightStabilityThreshold * 2;
                    var hasEnoughDataPoints = validDataPoints.Count >= minDataPointsRequired;
                    var isStable = rangeStable && hasEnoughDataPoints;
                    var stableWeight = isStable ? (min + max) / 2 : (decimal?)null;


                    _logger?.LogDebug(
                        $"Weight stability: {isStable} (range: {range:F3} kg, min: {min:F3}, max: {max:F3}, stableWeight: {stableWeight:F3}, validDataPoints: {validDataPoints.Count}/{minDataPointsRequired} (total: {buffer.Count}), rangeStable: {rangeStable}, hasEnoughData: {hasEnoughDataPoints})");

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
    ///     创建状态流
    /// </summary>
    private IObservable<AttendedWeighingStatus> CreateStatusStream(
        IObservable<decimal> weightStream,
        IObservable<WeightStabilityInfo> stabilityStream)
    {
        // 稳定性触发的状态转换（完全在流中处理，避免竞态条件）
        // 使用 _statusSubject 作为状态源，而不是 baseStatusStream，避免状态不同步问题
        // 使用 DistinctUntilChanged 确保 recordId 变化能触发状态转换
        var recordIdStream = _lastCreatedWeighingRecordIdSubject
            .DistinctUntilChanged(); // 只在 recordId 变化时发出

        return _statusSubject
            .CombineLatest(
                weightStream,
                stabilityStream,
                recordIdStream,
                (status, weight, stability, recordId) =>
                {
                    // 关键修复：如果已创建记录，强制使用正确的状态
                    if (recordId != null && weight > _minWeightThreshold)
                    {
                        // 如果已创建记录，应该保持在 WaitingForDeparture
                        if (status == AttendedWeighingStatus.WeightStabilized ||
                            status == AttendedWeighingStatus.WaitingForDeparture ||
                            status == AttendedWeighingStatus.WaitingForStability) // 防止状态不同步
                        {
                            _logger.LogDebug(
                                $"Forcing WaitingForDeparture: recordId={recordId}, currentStatus={status}, weight={weight:F3}t");
                            return AttendedWeighingStatus.WaitingForDeparture;
                        }
                    }

                    // 基于重量的状态转换（与 baseStatusStream 的逻辑一致）
                    var newStatus = status switch
                    {
                        // 上磅：OffScale -> WaitingForStability
                        AttendedWeighingStatus.OffScale when weight > _minWeightThreshold
                            => AttendedWeighingStatus.WaitingForStability,
                        // 异常下磅1：WaitingForStability -> OffScale (未稳定就下磅)
                        AttendedWeighingStatus.WaitingForStability when weight < _minWeightThreshold
                            => AttendedWeighingStatus.OffScale,
                        // 异常下磅2：WeightStabilized -> OffScale (稳定后突然下磅，跳过WaitingForDeparture)
                        AttendedWeighingStatus.WeightStabilized when weight < _minWeightThreshold
                            => AttendedWeighingStatus.OffScale,
                        // 正常下磅：WaitingForDeparture -> OffScale
                        AttendedWeighingStatus.WaitingForDeparture when weight < _minWeightThreshold
                            => AttendedWeighingStatus.OffScale,
                        _ => status // No state change
                    };

                    // 稳定性触发的状态转换
                    // 上磅阶段：WaitingForStability -> WeightStabilized
                    if (newStatus == AttendedWeighingStatus.WaitingForStability &&
                        stability.IsStable &&
                        recordId == null) // 检查是否已经称重过（null表示未称重）
                    {
                        _logger?.LogInformation(
                            $"Converting WaitingForStability -> WeightStabilized: weight={weight:F3}t, stability.IsStable={stability.IsStable}");
                        return AttendedWeighingStatus.WeightStabilized;
                    }

                    // 下磅阶段：WeightStabilized -> WaitingForDeparture
                    if (newStatus == AttendedWeighingStatus.WeightStabilized &&
                        weight > _minWeightThreshold &&
                        recordId != null) // 已经创建了称重记录
                    {
                        _logger?.LogInformation(
                            $"Converting WeightStabilized -> WaitingForDeparture: recordId={recordId}, weight={weight:F3}t");
                        return AttendedWeighingStatus.WaitingForDeparture;
                    }

                    return newStatus;
                })
            .DistinctUntilChanged();
    }

    /// <summary>
    ///     订阅状态变化（包含错误处理和重试机制）
    /// </summary>
    private IDisposable SubscribeToStatusChanges(
        IObservable<(AttendedWeighingStatus Status, decimal Weight, WeightStabilityInfo Stability)> combinedStream)
    {
        return combinedStream
            .Catch((Exception ex) =>
            {
                _logger?.LogError(ex, "Error in status stream, will retry in 5 seconds");
                // 延迟后重新订阅（通过返回空流触发重试）
                return Observable.Timer(TimeSpan.FromSeconds(5))
                    .SelectMany(_ =>
                        Observable
                            .Empty<(AttendedWeighingStatus Status, decimal Weight, WeightStabilityInfo Stability)>());
            })
            .Retry(3) // 最多重试3次
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(
                tuple =>
                {
                    // 调用 OnWeightAndStatusChanged 处理状态变化和副作用
                    OnWeightAndStatusChanged(tuple.Status, tuple.Weight, tuple.Stability);
                },
                error =>
                {
                    _logger?.LogError(error, "Fatal error in status stream subscription after retries");
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
    private void OnWeightAndStatusChanged(AttendedWeighingStatus newStatus, decimal weight,
        WeightStabilityInfo stability)
    {
        var previousStatus = _statusSubject.Value;

        // 处理状态转换的副作用（状态转换已在流中完成）
        if (newStatus != previousStatus)
        {
            _logger?.LogInformation(
                $"Status changed {previousStatus} -> {newStatus}, current weight: {weight}t");

            // 关键修复：当状态从 WaitingForStability 转为 WeightStabilized 时，立即创建记录
            // 不依赖 IsStable 状态，因为状态转换已经发生，说明之前已经满足稳定条件
            if (previousStatus == AttendedWeighingStatus.WaitingForStability &&
                newStatus == AttendedWeighingStatus.WeightStabilized &&
                _lastCreatedWeighingRecordIdSubject.Value == null)
            {
                // 使用稳定重量（如果可用），否则使用当前重量
                var weightToUse = stability.StableWeight ?? weight;
                _logger?.LogInformation(
                    $"Weight stabilized (status transition), creating record with weight: {weightToUse:F3}t");

                // 立即创建称重记录
                EnqueueAsyncOperation(async () => await OnWeightStabilizedAsync(weightToUse));
            }

            // 处理状态转换的其他副作用
            ProcessStatusTransition(previousStatus, newStatus, weight);

            // 更新状态并发送通知（状态已在流中更新，这里同步 Subject）
            UpdateStatusAndNotify(newStatus);
        }

        // 备用检查：如果状态已经是 WeightStabilized 但记录还未创建（防止状态转换时遗漏）
        // 这主要处理状态已经是 WeightStabilized 但之前没有创建记录的情况
        // 关键：必须同时检查 IsStable，确保只有在稳定时才创建记录
        if (newStatus == AttendedWeighingStatus.WeightStabilized &&
            stability.IsStable && // 必须稳定才创建记录
            _lastCreatedWeighingRecordIdSubject.Value == null) // 检查是否已经称重过（null表示未称重）
        {
            // Weight stabilized - use stable weight (average) if available
            var weightToUse = stability.StableWeight ?? weight;
            _logger?.LogInformation(
                $"Weight stabilized (backup check), stable weight: {weightToUse}t");

            // When weight is stabilized, capture photos and create WeighingRecord
            EnqueueAsyncOperation(async () => await OnWeightStabilizedAsync(weightToUse));
        }
    }

    /// <summary>
    ///     更新状态并发送通知
    /// </summary>
    private void UpdateStatusAndNotify(AttendedWeighingStatus newStatus)
    {
        _statusSubject.OnNext(newStatus);
        MessageBus.Current.SendMessage(new StatusChangedMessage(newStatus));
    }

    /// <summary>
    ///     获取状态对应的语音播报文案
    /// </summary>
    private string GetStatusAudioText(AttendedWeighingStatus previousStatus, AttendedWeighingStatus currentStatus)
    {
        // 特殊处理：WaitingForDeparture进入OffScale时
        if (previousStatus == AttendedWeighingStatus.WaitingForDeparture &&
            currentStatus == AttendedWeighingStatus.OffScale)
        {
            return "车辆已下磅，称重已完成";
        }
        
        if (previousStatus == AttendedWeighingStatus.WaitingForStability &&
            currentStatus == AttendedWeighingStatus.OffScale)
        {
            return "车辆已下磅";
        }


        if (previousStatus == AttendedWeighingStatus.OffScale &&
            currentStatus == AttendedWeighingStatus.WaitingForStability)
        {
            return "车辆已上磅，正在称重";
        }

        // 根据当前状态返回对应文案
        return currentStatus switch
        {
            AttendedWeighingStatus.WeightStabilized => "称重已结束",
            _ => string.Empty
        };
    }

    /// <summary>
    ///     处理状态转换的副作用
    /// </summary>
    private void ProcessStatusTransition(
        AttendedWeighingStatus previousStatus,
        AttendedWeighingStatus newStatus,
        decimal weight)
    {
        // 播放状态变化语音提示
        EnqueueAsyncOperation(async () =>
        {
            if (_soundDeviceService != null)
            {
                try
                {
                    var statusDescription = GetStatusAudioText(previousStatus, newStatus);
                    if (string.IsNullOrEmpty(statusDescription))
                    {
                        return;
                    }

                    await _soundDeviceService.PlayTextV2Async(statusDescription);
                    _logger?.LogDebug($"Played status change audio: {statusDescription}");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to play status change audio");
                }
            }
        });

        // 处理状态转换的副作用
        switch (previousStatus, newStatus)
        {
            case (AttendedWeighingStatus.OffScale, AttendedWeighingStatus.WaitingForStability):
                // 上磅：进入等待稳定状态
                _logger.LogInformation(
                    $"Entered WaitingForStability state (ascending), weight: {weight:F3}t");

                // 触发 LPRAllInOne 抓拍（如果配置为 LPRAllInOne）
                EnqueueAsyncOperation(async () => await TriggerCaptureOnWaitingForStabilityAsync());
                break;

            case (AttendedWeighingStatus.WaitingForStability, AttendedWeighingStatus.WeightStabilized):
                // 正常流程：重量已稳定
                // 注意：记录创建已在 OnWeightAndStatusChanged 中处理，这里只记录日志
                _logger?.LogInformation(
                    $"Entered WeightStabilized state (weight stabilized), weight: {weight:F3}t");
                break;

            case (AttendedWeighingStatus.WaitingForStability, AttendedWeighingStatus.OffScale):
                // 异常流程：未稳定就下磅
                _logger?.LogWarning(
                    $"Unstable weighing flow (abnormal departure), weight returned to {weight:F3}t, triggered capture");

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
                    $"Entered WaitingForDeparture state (descending), weight: {weight:F3}t");
                break;

            case (AttendedWeighingStatus.WeightStabilized, AttendedWeighingStatus.OffScale):
                // 异常流程：稳定后突然下磅（跳过WaitingForDeparture）
                _logger?.LogWarning(
                    $"Abnormal departure from WeightStabilized, weight returned to {weight:F3}t");

                // 触发 LPRAllInOne 抓拍（如果配置为 LPRAllInOne）
                EnqueueAsyncOperation(async () => await TriggerCaptureOnOffScaleAsync());
                EnqueueAsyncOperation(async () => await ResetWeighingCycleAsync());
                break;

            case (AttendedWeighingStatus.WaitingForDeparture, AttendedWeighingStatus.OffScale):
                // 正常流程：正常下磅完成
                _logger?.LogInformation(
                    $"Normal flow completed (normal departure), entered OffScale state, weight: {weight:F3}t");

                // 触发 LPRAllInOne 抓拍（如果配置为 LPRAllInOne）
                EnqueueAsyncOperation(async () => await TriggerCaptureOnOffScaleAsync());
                EnqueueAsyncOperation(async () => await ResetWeighingCycleAsync());
                break;
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
        _lastCreatedWeighingRecordIdSubject.OnNext(null);
    }


    /// <summary>
    ///     重量已稳定时的处理
    /// </summary>
    private async Task OnWeightStabilizedAsync(decimal currentWeight)
    {
        try
        {
            // Capture all cameras (Hikvision)
            var photoPaths = await CaptureAllCamerasAsync("WeightStabilized");

            // 触发 LPRAllInOne 车牌识别（方法内部会判断 LprDeviceType）
            await TriggerCaptureOnWeightStabilizedAsync();

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
            // FIX: Use absolute path to ensure photos are saved to application directory
            // when launched from any working directory (e.g., C:\Windows\System32)
            var basePath = AttachmentPathUtils.GetLocalStorageAbsolutePath(AttachType.EntryPhoto, now);

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
    ///     触发 LPRAllInOne 抓拍（进入 WaitingForStability 状态时）
    /// </summary>
    private async Task TriggerCaptureOnWaitingForStabilityAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();

            // 如果类型不是 LprAllInOne，不做任何动作
            if (settings.SystemSettings.LprDeviceType != LprDeviceType.LprAllInOne)
            {
                return;
            }

            // 如果服务未注入，记录警告并返回
            if (_lprAllInOneService == null)
            {
                _logger?.LogWarning(
                    "ILPRAllInOneService is not available, cannot trigger capture on WaitingForStability");
                return;
            }

            var lprConfigs = settings.LicensePlateRecognitionConfigs;
            if (lprConfigs.Count == 0)
            {
                _logger?.LogWarning("No LPRAllInOne devices configured, cannot trigger capture on WaitingForStability");
                return;
            }

            _logger?.LogInformation("Triggering LPRAllInOne capture on WaitingForStability for {Count} devices",
                lprConfigs.Count);

            var tasks = lprConfigs
                .Where(config => config.IsValid())
                .Select(async config =>
                {
                    var success = await _lprAllInOneService.TriggerManualRecognitionAsync(config);
                    if (success)
                    {
                        _logger?.LogInformation(
                            "Successfully triggered LPRAllInOne capture for device: {Name} ({Ip}) on WaitingForStability",
                            config.Name, config.Ip);
                    }
                    else
                    {
                        _logger?.LogWarning(
                            "Failed to trigger LPRAllInOne capture for device: {Name} ({Ip}) on WaitingForStability",
                            config.Name, config.Ip);
                    }

                    return success;
                });

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);
            var failCount = results.Length - successCount;

            _logger?.LogInformation(
                "LPRAllInOne capture on WaitingForStability completed: {SuccessCount} succeeded, {FailCount} failed",
                successCount, failCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error occurred while triggering LPRAllInOne capture on WaitingForStability");
        }
    }

    /// <summary>
    ///     触发 LPRAllInOne 抓拍（进入 WeightStabilized 状态时）
    /// </summary>
    private async Task TriggerCaptureOnWeightStabilizedAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();

            // 如果类型不是 LprAllInOne，不做任何动作
            if (settings.SystemSettings.LprDeviceType != LprDeviceType.LprAllInOne)
            {
                return;
            }

            // 如果服务未注入，记录警告并返回
            if (_lprAllInOneService == null)
            {
                _logger?.LogWarning("ILPRAllInOneService is not available, cannot trigger capture on WeightStabilized");
                return;
            }

            var lprConfigs = settings.LicensePlateRecognitionConfigs;
            if (lprConfigs.Count == 0)
            {
                _logger?.LogWarning("No LPRAllInOne devices configured, cannot trigger capture on WeightStabilized");
                return;
            }

            _logger?.LogInformation("Triggering LPRAllInOne capture on WeightStabilized for {Count} devices",
                lprConfigs.Count);

            var tasks = lprConfigs
                .Where(config => config.IsValid())
                .Select(async config =>
                {
                    var success = await _lprAllInOneService.TriggerManualRecognitionAsync(config);
                    if (success)
                    {
                        _logger?.LogInformation(
                            "Successfully triggered LPRAllInOne capture for device: {Name} ({Ip}) on WeightStabilized",
                            config.Name, config.Ip);
                    }
                    else
                    {
                        _logger?.LogWarning(
                            "Failed to trigger LPRAllInOne capture for device: {Name} ({Ip}) on WeightStabilized",
                            config.Name, config.Ip);
                    }

                    return success;
                });

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);
            var failCount = results.Length - successCount;

            _logger?.LogInformation(
                "LPRAllInOne capture on WeightStabilized completed: {SuccessCount} succeeded, {FailCount} failed",
                successCount, failCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error occurred while triggering LPRAllInOne capture on WeightStabilized");
        }
    }

    /// <summary>
    ///     触发 LPRAllInOne 抓拍（进入 OffScale 状态时）
    /// </summary>
    private async Task TriggerCaptureOnOffScaleAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();

            // 如果类型不是 LprAllInOne，不做任何动作
            if (settings.SystemSettings.LprDeviceType != LprDeviceType.LprAllInOne)
            {
                return;
            }

            // 如果服务未注入，记录警告并返回
            if (_lprAllInOneService == null)
            {
                _logger?.LogWarning("ILPRAllInOneService is not available, cannot trigger capture on OffScale");
                return;
            }

            var lprConfigs = settings.LicensePlateRecognitionConfigs;
            if (lprConfigs.Count == 0)
            {
                _logger?.LogWarning("No LPRAllInOne devices configured, cannot trigger capture on OffScale");
                return;
            }

            _logger?.LogInformation("Triggering LPRAllInOne capture on OffScale for {Count} devices", lprConfigs.Count);

            var tasks = lprConfigs
                .Where(config => config.IsValid())
                .Select(async config =>
                {
                    var success = await _lprAllInOneService.TriggerManualRecognitionAsync(config);
                    if (success)
                    {
                        _logger?.LogInformation(
                            "Successfully triggered LPRAllInOne capture for device: {Name} ({Ip}) on OffScale",
                            config.Name, config.Ip);
                    }
                    else
                    {
                        _logger?.LogWarning(
                            "Failed to trigger LPRAllInOne capture for device: {Name} ({Ip}) on OffScale",
                            config.Name, config.Ip);
                    }

                    return success;
                });

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);
            var failCount = results.Length - successCount;

            _logger?.LogInformation(
                "LPRAllInOne capture on OffScale completed: {SuccessCount} succeeded, {FailCount} failed",
                successCount, failCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error occurred while triggering LPRAllInOne capture on OffScale");
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
            var currentDeliveryType = _deliveryTypeSubject.Value;
            var weighingRecord = new WeighingRecord(weight, plateNumber);
            weighingRecord.DeliveryType = currentDeliveryType;
            
            // Set WeighingMode from settings
            var weighingMode = await _settingsService.GetWeighingModeAsync();
            weighingRecord.SetWeighingMode(weighingMode);
            
            await _weighingRecordRepository.InsertAsync(weighingRecord);
            await uow.CompleteAsync();

            _logger?.LogInformation(
                $"Created weighing record successfully, ID: {weighingRecord.Id}, Weight: {weight}t, PlateNumber: {plateNumber ?? "None"}, DeliveryType: {currentDeliveryType}");

            // 保存最近创建的称重记录ID，用于后续重写车牌号
            _lastCreatedWeighingRecordIdSubject.OnNext(weighingRecord.Id);

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

                    // Storage: Convert to relative path for database portability (migration-friendly)
                    var fileName = Path.GetFileName(photoPath);
                    var relativePath = PathManager.ToRelativePath(photoPath);
                    var attachmentFile = new AttachmentFile(fileName, relativePath, AttachType.UnmatchedEntryPhoto);

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
    ///     尝试重写称重记录的车牌号和收发类型
    ///     在清空车牌缓存前调用，用最频繁识别的车牌号更新最近创建的称重记录
    /// </summary>
    private async Task TryReWritePlateNumberAsync()
    {
        // Get latest record ID directly from subject
        var recordId = _lastCreatedWeighingRecordIdSubject.Value;

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

            var currentDeliveryType = _deliveryTypeSubject.Value;
            var hasChanges = false;

            if (weighingRecord.PlateNumber != plateNumber)
            {
                var oldPlateNumber = weighingRecord.PlateNumber;
                weighingRecord.PlateNumber = plateNumber;
                hasChanges = true;

                _logger?.LogInformation(
                    $"Rewrote plate number for weighing record {weighingRecord.Id}, from '{oldPlateNumber ?? "None"}' to '{plateNumber}'");

                // 通过 ReactiveUI MessageBus 发送更新车牌号消息
                var updateMessage = new UpdatePlateNumberMessage(weighingRecord.Id, plateNumber);
                MessageBus.Current.SendMessage(updateMessage);

                _logger?.LogInformation(
                    " Sent UpdatePlateNumberMessage via MessageBus for WeighingRecordId {RecordId}, PlateNumber {PlateNumber}",
                    weighingRecord.Id, plateNumber);
            }

            if (weighingRecord.DeliveryType != currentDeliveryType)
            {
                var oldDeliveryType = weighingRecord.DeliveryType;
                weighingRecord.DeliveryType = currentDeliveryType;
                hasChanges = true;

                _logger?.LogInformation(
                    $"Rewrote delivery type for weighing record {weighingRecord.Id}, from '{oldDeliveryType}' to '{currentDeliveryType}'");
            }

            if (hasChanges)
            {
                await _weighingRecordRepository.UpdateAsync(weighingRecord);
                await uow.CompleteAsync();
                await _localEventBus.PublishAsync(new TryMatchEvent(weighingRecord.Id));
            }
            else
            {
                await uow.CompleteAsync();
                _logger?.LogDebug(
                    $"Plate number and delivery type unchanged for weighing record {recordId.Value}");
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
        _plateNumberCache.Clear();
        _logger?.LogDebug("Cleared plate number cache");

        // Notify observers that plate number is cleared via MessageBus
        var message = new PlateNumberChangedMessage(null);
        MessageBus.Current.SendMessage(message);
    }
}