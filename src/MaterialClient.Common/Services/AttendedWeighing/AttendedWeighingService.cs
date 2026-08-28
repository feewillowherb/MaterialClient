using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services.AttendedWeighing.Records;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Vzvision;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Common.Services.AttendedWeighing;

/// <summary>
///     有人值守称重服务
///     实现 IAttendedWeighingService 接口，协调各子服务完成称重流程
/// </summary>
public class AttendedWeighingService : IAttendedWeighingService, ISingletonDependency
{
    private readonly WeighingStateManager _stateManager;
    private readonly IPlateNumberService _plateNumberService;
    private readonly IWeighingStreamPipeline _streamPipeline;
    private readonly IWeighingCaptureService _captureService;
    private readonly IWeighingRecordService _recordService;
    private readonly ILogger<AttendedWeighingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ILocalEventBus _localEventBus;
    private readonly ISettingsService _settingsService;
    private readonly ISoundDeviceService? _soundDeviceService;
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly IWeighingPipelineStrategy _pipelineStrategy;

    // Configuration fields
    private decimal _minWeightThreshold;
    private decimal _weightStabilityThreshold;
    private int _stabilityWindowMs;
    private int _stabilityCheckIntervalMs;
    private bool _enableLatestPlateNumber;
    private bool _enablePlateRewrite;
    private bool _enableMatchOnStable;

    // Subscription management
    private IDisposable? _stateSubscription;
    private IDisposable? _licensePlateSubscription;
    private IDisposable? _ghostGateSessionSubscription;
    private IDisposable? _settingsSavedSubscription;

    // Async operation tracking
    private readonly ConcurrentBag<Task> _pendingOperations = new();
    private readonly object _operationsLock = new();
    private Subject<Func<Task>>? _asyncOperationsStream;
    private IDisposable? _asyncOperationsSubscription;

    public AttendedWeighingService(
        WeighingStateManager stateManager,
        IPlateNumberService plateNumberService,
        IWeighingStreamPipeline streamPipeline,
        IWeighingCaptureService captureService,
        IWeighingRecordService recordService,
        ITruckScaleWeightService truckScaleWeightService,
        ILogger<AttendedWeighingService> logger,
        IConfiguration configuration,
        ILocalEventBus localEventBus,
        ISettingsService settingsService,
        IWeighingPipelineStrategy? pipelineStrategy = null,
        ISoundDeviceService? soundDeviceService = null)
    {
        _stateManager = stateManager;
        _plateNumberService = plateNumberService;
        _streamPipeline = streamPipeline;
        _captureService = captureService;
        _recordService = recordService;
        _truckScaleWeightService = truckScaleWeightService;
        _logger = logger;
        _configuration = configuration;
        _localEventBus = localEventBus;
        _settingsService = settingsService;
        _pipelineStrategy = pipelineStrategy ?? new DefaultWeighingPipelineStrategy(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultWeighingPipelineStrategy>());
        _soundDeviceService = soundDeviceService;
    }

    /// <inheritdoc />
    public DeliveryType CurrentDeliveryType => _stateManager.CurrentDeliveryType;

    /// <inheritdoc />
    public async Task StartAsync()
    {
        await LoadConfigurationAsync();

        if (_stateSubscription != null) return; // Already started

        // Subscribe to ILocalEventBus events
        if (_licensePlateSubscription == null)
        {
            _licensePlateSubscription = _localEventBus.Subscribe<LicensePlateRecognizedEventData>(async eventData =>
            {
                try
                {
                    _logger.LogInformation(
                        "收到 LPR 事件: {Plate} 来自 {Device} (类型: {DeviceType})",
                        eventData.PlateNumber, eventData.DeviceName, eventData.DeviceType);

                    var settings = await _settingsService.GetSettingsAsync();
                    var lprRow = LicensePlateRecognitionConfig.FindByDeviceName(
                        settings.LicensePlateRecognitionConfigs,
                        eventData.DeviceName);
                    if (lprRow is { SiteType: not LprSiteType.Scale })
                    {
                        _logger.LogInformation(
                            "Skip weighing LPR handling for site type {SiteType} device {Device}",
                            lprRow.SiteType,
                            eventData.DeviceName);
                        return;
                    }

                    _plateNumberService.OnPlateNumberRecognized(eventData.PlateNumber, eventData.ColorType);

                    if (!string.IsNullOrWhiteSpace(eventData.LprImagePath))
                    {
                        var candidate = new CycleLprCandidate(
                            eventData.LprImagePath,
                            !string.IsNullOrWhiteSpace(eventData.PlateNumber),
                            eventData.Timestamp == default ? DateTime.Now : eventData.Timestamp);

                        if (_stateManager.TryAcceptLprCandidate(candidate))
                        {
                            var recordId = _stateManager.GetLastCreatedWeighingRecordId();
                            if (recordId is > 0)
                            {
                                var accepted = _stateManager.GetCurrentCycleLprCandidate();
                                if (accepted is not null)
                                    await _recordService.UpsertLprAttachmentAsync(recordId.Value, accepted);
                            }
                        }
                    }

                    // 存储车辆信息到状态管理器
                    _stateManager.SetCurrentCycleVehicleInfo(eventData.VehicleColor, eventData.VehicleType,
                        eventData.PlateColor);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理 LPR 事件失败: {Plate}", eventData.PlateNumber);
                }
            });

            _logger.LogInformation("已订阅 LicensePlateRecognizedEventData (ILocalEventBus)");
        }

        if (_ghostGateSessionSubscription == null)
        {
            _ghostGateSessionSubscription =
                _localEventBus.Subscribe<GhostGateSessionResetEventData>(async eventData =>
                {
                    try
                    {
                        _plateNumberService.RemovePlate(eventData.AbandonedPlateNumber);
                        var mostFrequent = _plateNumberService.GetMostFrequentPlateNumber();
                        await _localEventBus.PublishAsync(new PlateNumberChangedEventData(mostFrequent));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "处理幽灵道闸会话重置事件失败: AbandonedPlate={AbandonedPlate}",
                            eventData.AbandonedPlateNumber);
                    }
                });

            _logger.LogInformation("已订阅 GhostGateSessionResetEventData (ILocalEventBus)");
        }

        if (_settingsSavedSubscription == null)
        {
            _settingsSavedSubscription = _localEventBus.Subscribe<SettingsSavedEventData>(async _ =>
            {
                EnqueueAsyncOperation(UpdateRuntimeConfigurationAsync);
            });
            _logger.LogInformation("已订阅 SettingsSavedEventData (ILocalEventBus)");
        }

        // Load configuration into fields
        var config = await GetConfigurationAsync();
        _minWeightThreshold = config.MinWeightThreshold;
        _weightStabilityThreshold = config.WeightStabilityThreshold;
        _stabilityWindowMs = config.StabilityWindowMs;
        _stabilityCheckIntervalMs = config.StabilityCheckIntervalMs;
        _enableLatestPlateNumber = config.EnableLatestPlateNumber;
        _enablePlateRewrite = config.EnablePlateRewrite;
        _enableMatchOnStable = config.EnableMatchOnStable;

        // Initialize plate color filter (once)
        var lowPriorityColors = _configuration.GetSection("LowPriorityPlateColors").Get<VzvisionColorType[]>();
        var colorSet = (lowPriorityColors == null || lowPriorityColors.Length == 0)
            ? new HashSet<VzvisionColorType>()
            : lowPriorityColors.Select(v => (VzvisionColorType)v).ToHashSet();
        _plateNumberService.InitializeColorFilter(colorSet);
        _plateNumberService.UpdateConfiguration(_enableLatestPlateNumber, _enablePlateRewrite);

        // Build stream pipeline
        var sharedWeightSource = _truckScaleWeightService.WeightUpdates
            .Publish()
            .RefCount();

        var statusStream = _streamPipeline.Build(sharedWeightSource, config, _stateManager);

        // Create weight and stability streams for combined processing
        var weightStream = sharedWeightSource
            .Buffer(TimeSpan.FromMilliseconds(config.StabilityCheckIntervalMs))
            .Where(buffer => buffer.Count > 0)
            .Select(buffer => buffer.Last())
            .StartWith(0m);

        var minDataPointsRequired =
            Math.Max(8, (int)(config.StabilityWindowMs / config.StabilityCheckIntervalMs * 0.5));

        var stabilityStream = sharedWeightSource
            .Buffer(TimeSpan.FromMilliseconds(config.StabilityWindowMs),
                TimeSpan.FromMilliseconds(config.StabilityCheckIntervalMs))
            .Select(buffer =>
            {
                if (buffer.Count > 0)
                {
                    var validDataPoints = buffer.Where(w => w > config.MinWeightThreshold).ToList();
                    if (validDataPoints.Count == 0)
                        return new WeightStabilityInfo { IsStable = false };

                    var min = validDataPoints.Min();
                    var max = validDataPoints.Max();
                    var range = max - min;
                    var isStable = range <= config.WeightStabilityThreshold * 2 &&
                                   validDataPoints.Count >= minDataPointsRequired;

                    return new WeightStabilityInfo
                    {
                        IsStable = isStable,
                        StableWeight = isStable ? (min + max) / 2 : null,
                        Min = min,
                        Max = max,
                        Range = range
                    };
                }

                return new WeightStabilityInfo { IsStable = false };
            })
            .StartWith(new WeightStabilityInfo { IsStable = false })
            .DistinctUntilChanged(info => info.IsStable)
            .Replay(1)
            .RefCount();

        // Subscribe to combined status stream
        var combinedStream = statusStream
            .CombineLatest(
                weightStream,
                stabilityStream,
                (status, weight, stability) => (Status: status, Weight: weight, Stability: stability))
            .DistinctUntilChanged(t => t.Status);

        _stateSubscription = combinedStream
            .Catch((Exception ex) =>
            {
                _logger.LogError(ex, "Error in status stream, will retry in 5 seconds");
                return Observable.Timer(TimeSpan.FromSeconds(5))
                    .SelectMany(_ =>
                        Observable.Empty<(AttendedWeighingStatus Status, decimal Weight, WeightStabilityInfo Stability)>());
            })
            .Retry(3)
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(
                tuple => OnWeightAndStatusChanged(tuple.Status, tuple.Weight, tuple.Stability),
                error => _logger.LogError(error, "Fatal error in status stream subscription after retries"));

        // Start async operation queue
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
                    _logger.LogError(ex, "Error in async operation");
                    return (Success: false, Error: (Exception?)ex);
                }
            }))
            .Merge(maxConcurrent: 5)
            .Catch((Exception ex) =>
            {
                _logger.LogError(ex, "Critical error in async operations stream");
                return Observable.Empty<(bool Success, Exception? Error)>();
            })
            .Retry(3)
            .Subscribe(
                result =>
                {
                    if (!result.Success)
                    {
                        _logger.LogWarning("Async operation failed, may need manual intervention");
                    }
                },
                error => { _logger.LogError(error, "Fatal error in async operations stream"); });

        _asyncOperationsStream = asyncOperationsStream;

        _logger.LogInformation("Started monitoring truck scale weight changes");

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        _stateSubscription?.Dispose();
        _stateSubscription = null;

        try
        {
            _asyncOperationsStream?.OnCompleted();
        }
        catch (InvalidOperationException)
        {
            // Stream already completed
        }

        _asyncOperationsStream?.Dispose();
        _asyncOperationsStream = null;
        _asyncOperationsSubscription?.Dispose();
        _asyncOperationsSubscription = null;

        var pendingTasks = _pendingOperations.Where(t => !t.IsCompleted).ToArray();
        if (pendingTasks.Length > 0)
        {
            _logger.LogInformation("Waiting for {Count} pending operations to complete...", pendingTasks.Length);

            try
            {
                var timeout = TimeSpan.FromMinutes(5);
                var allTasksCompleted = Task.WhenAll(pendingTasks);
                var timeoutTask = Task.Delay(timeout);
                var completed = await Task.WhenAny(allTasksCompleted, timeoutTask);

                if (completed == allTasksCompleted)
                {
                    _logger.LogInformation("All pending operations completed");
                }
                else
                {
                    _logger.LogWarning(
                        "Timeout waiting for operations to complete. {Count} operations may still be running.",
                        pendingTasks.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while waiting for pending operations");
            }
        }

        lock (_operationsLock)
        {
            var remainingTasks = _pendingOperations.Where(t => !t.IsCompleted).ToList();
            _pendingOperations.Clear();
            foreach (var remainingTask in remainingTasks)
            {
                _pendingOperations.Add(remainingTask);
            }
        }

        _logger.LogInformation("Stopped monitoring truck scale weight changes");

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public AttendedWeighingStatus GetCurrentStatus() => _stateManager.GetCurrentStatus();

    /// <inheritdoc />
    public string? GetMostFrequentPlateNumber() => _plateNumberService.GetMostFrequentPlateNumber();

    /// <inheritdoc />
    public void SetDeliveryType(DeliveryType deliveryType) => _stateManager.SetDeliveryType(deliveryType);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        try
        {
            _licensePlateSubscription?.Dispose();
            _licensePlateSubscription = null;
            _ghostGateSessionSubscription?.Dispose();
            _ghostGateSessionSubscription = null;
            _settingsSavedSubscription?.Dispose();
            _settingsSavedSubscription = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "释放 ILocalEventBus 订阅时发生异常");
        }

        // WeighingStateManager handles its own subject disposal
        _stateManager.Dispose();
    }

    private void OnWeightAndStatusChanged(AttendedWeighingStatus newStatus, decimal weight,
        WeightStabilityInfo stability)
    {
        var previousStatus = _stateManager.GetCurrentStatus();

        if (newStatus != previousStatus)
        {
            _logger.LogDebug("Status changed {PreviousStatus} -> {NewStatus}, current weight: {Weight}t",
                previousStatus, newStatus, weight);

            // When transitioning from WaitingForStability → WeightStabilized, create record immediately
            if (previousStatus == AttendedWeighingStatus.WaitingForStability &&
                newStatus == AttendedWeighingStatus.WeightStabilized &&
                _stateManager.GetLastCreatedWeighingRecordId() == null)
            {
                var weightToUse = stability.StableWeight ?? weight;
                _logger.LogInformation(
                    "Weight stabilized (status transition), creating record with weight: {Weight:F3}t",
                    weightToUse);

                EnqueueAsyncOperation(async () => await OnWeightStabilizedAsync(weightToUse));
            }

            ProcessStatusTransition(previousStatus, newStatus, weight);

            _stateManager.UpdateStatusAndNotify(newStatus);
        }

        // Backup check: if already WeightStabilized but no record created yet
        if (newStatus == AttendedWeighingStatus.WeightStabilized &&
            stability.IsStable &&
            _stateManager.GetLastCreatedWeighingRecordId() == null)
        {
            var weightToUse = stability.StableWeight ?? weight;
            _logger.LogInformation("Weight stabilized (backup check), stable weight: {Weight}t", weightToUse);

            EnqueueAsyncOperation(async () => await OnWeightStabilizedAsync(weightToUse));
        }
    }

    private async Task OnWeightStabilizedAsync(decimal currentWeight)
    {
        try
        {
            var photoPaths = await _captureService.CaptureAllCamerasAsync("WeightStabilized");
            await _recordService.CreateWeighingRecordAsync(currentWeight, photoPaths, _stateManager);
            await _captureService.CaptureOnWeightStabilized();
            await _recordService.TryPublishMatchOnStableAsync(_stateManager);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while processing weight stabilization");
        }
    }

    private void ProcessStatusTransition(
        AttendedWeighingStatus previousStatus,
        AttendedWeighingStatus newStatus,
        decimal weight)
    {
        // Strategy extension point for mode-specific behavior
        EnqueueAsyncOperation(async () =>
            await _pipelineStrategy.OnStatusTransitionAsync(previousStatus, newStatus, weight));

        // Play audio announcements
        EnqueueAsyncOperation(async () =>
        {
            if (_soundDeviceService != null)
            {
                try
                {
                    var statusDescription = GetStatusAudioText(previousStatus, newStatus);
                    if (!string.IsNullOrEmpty(statusDescription))
                    {
                        await _soundDeviceService.PlayTextV2Async(statusDescription);
                        _logger.LogDebug("Played status change audio: {Description}", statusDescription);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to play status change audio");
                }
            }
        });

        switch (previousStatus, newStatus)
        {
            case (AttendedWeighingStatus.OffScale, AttendedWeighingStatus.WaitingForStability):
                _logger.LogInformation("Entered WaitingForStability state (ascending), weight: {Weight:F3}t",
                    weight);
                break;

            case (AttendedWeighingStatus.WaitingForStability, AttendedWeighingStatus.WeightStabilized):
                _logger.LogInformation(
                    "Entered WeightStabilized state (weight stabilized), weight: {Weight:F3}t", weight);
                break;

            case (AttendedWeighingStatus.WaitingForStability, AttendedWeighingStatus.OffScale):
                _logger.LogWarning(
                    "Unstable weighing flow (abnormal departure), weight returned to {Weight:F3}t, triggered capture",
                    weight);
                EnqueueAsyncOperation(async () =>
                {
                    var photos = await _captureService.CaptureAllCamerasAsync("UnstableWeighingFlow");
                    if (photos.Count == 0)
                        _logger.LogWarning(
                            "Unstable weighing flow capture completed, but no photos were obtained");
                    else
                        _logger.LogInformation("Unstable weighing flow captured {Count} photos", photos.Count);
                });
                EnqueueAsyncOperation(async () =>
                    await _recordService.RewriteAndResetCycleAsync(_stateManager, _plateNumberService));
                break;

            case (AttendedWeighingStatus.WeightStabilized, AttendedWeighingStatus.WaitingForDeparture):
                _logger.LogInformation("Entered WaitingForDeparture state (descending), weight: {Weight:F3}t",
                    weight);
                break;

            case (AttendedWeighingStatus.WeightStabilized, AttendedWeighingStatus.OffScale):
                _logger.LogWarning(
                    "Abnormal departure from WeightStabilized, weight returned to {Weight:F3}t", weight);
                EnqueueAsyncOperation(async () =>
                    await _recordService.RewriteAndResetCycleAsync(_stateManager, _plateNumberService));
                break;

            case (AttendedWeighingStatus.WaitingForDeparture, AttendedWeighingStatus.OffScale):
                _logger.LogInformation(
                    "Normal flow completed (normal departure), entered OffScale state, weight: {Weight:F3}t",
                    weight);
                EnqueueAsyncOperation(async () =>
                    await _recordService.RewriteAndResetCycleAsync(_stateManager, _plateNumberService));
                break;
        }
    }

    private static string GetStatusAudioText(AttendedWeighingStatus previousStatus,
        AttendedWeighingStatus currentStatus)
    {
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

        return currentStatus switch
        {
            AttendedWeighingStatus.WeightStabilized => "称重已结束",
            _ => string.Empty
        };
    }

    private void EnqueueAsyncOperation(Func<Task> operation)
    {
        if (_asyncOperationsStream == null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await operation();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in async operation (fallback mode)");
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
            _logger.LogWarning("Async operations stream is closed, using fallback Task.Run");
            _ = Task.Run(async () =>
            {
                try
                {
                    await operation();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in async operation (fallback mode)");
                }
            });
        }
    }

    private async Task LoadConfigurationAsync()
    {
        try
        {
            var config = await GetConfigurationAsync();
            _logger.LogInformation(
                "Loaded configuration - MinWeightThreshold: {MinWeight}, WeightStabilityThreshold: {StabilityThreshold}, " +
                "StabilityWindowMs: {WindowMs}, StabilityCheckIntervalMs: {IntervalMs}",
                config.MinWeightThreshold, config.WeightStabilityThreshold, config.StabilityWindowMs,
                config.StabilityCheckIntervalMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load configuration, using default values");
        }
    }

    private async Task<WeighingConfiguration> GetConfigurationAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            return settings.WeighingConfiguration;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load configuration, using default values");
            return new WeighingConfiguration();
        }
    }

    private async Task UpdateRuntimeConfigurationAsync()
    {
        try
        {
            var config = await GetConfigurationAsync();
            _enableLatestPlateNumber = config.EnableLatestPlateNumber;
            _enablePlateRewrite = config.EnablePlateRewrite;
            _enableMatchOnStable = config.EnableMatchOnStable;
            _plateNumberService.UpdateConfiguration(_enableLatestPlateNumber, _enablePlateRewrite);
            _logger.LogInformation(
                "已刷新称重运行时开关: EnableLatestPlateNumber={LatestPlate}, EnablePlateRewrite={PlateRewrite}, EnableMatchOnStable={MatchOnStable}",
                _enableLatestPlateNumber, _enablePlateRewrite, _enableMatchOnStable);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "刷新运行时称重配置失败");
        }
    }
}
