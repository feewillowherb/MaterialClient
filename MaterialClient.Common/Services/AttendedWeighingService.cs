using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
internal record WeightStabilityInfo
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
    ///     Observable stream of status changes
    /// </summary>
    IObservable<AttendedWeighingStatus> StatusChanges { get; }

    /// <summary>
    ///     Observable stream of most frequent plate number changes
    /// </summary>
    IObservable<string?> MostFrequentPlateNumberChanges { get; }

    /// <summary>
    ///     Check if weight is stable (changes less than ±0.1m within 3 seconds)
    /// </summary>
    bool IsWeightStable { get; }

    /// <summary>
    ///     Observable stream of new weighing record creation events
    /// </summary>
    IObservable<WeighingRecord> WeighingRecordCreated { get; }

    /// <summary>
    ///     获取当前收发料类型
    /// </summary>
    DeliveryType CurrentDeliveryType { get; }

    /// <summary>
    ///     Observable stream of delivery type changes
    /// </summary>
    IObservable<DeliveryType> DeliveryTypeChanges { get; }

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
    // 称重配置（从设置中加载）
    private decimal _minWeightThreshold = 0.5m; // 0.5t = 500kg
    private decimal _weightStabilityThreshold = 0.05m; // ±0.05m = 0.1m total range
    private int _stabilityWindowMs = 3000;
    private int _stabilityCheckIntervalMs = 200; // 默认 200ms

    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;

    private readonly IHikvisionService _hikvisionService;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<AttendedWeighingService> _logger;

    // 车牌识别缓存
    private readonly ConcurrentDictionary<string, PlateNumberCacheRecord> _plateNumberCache = new();

    // Rx Subject for plate number updates
    private readonly Subject<string?> _plateNumberSubject = new();
    private readonly ISettingsService _settingsService;

    // Rx Subject for status updates - using BehaviorSubject to maintain current state
    private readonly BehaviorSubject<AttendedWeighingStatus> _statusSubject = new(AttendedWeighingStatus.OffScale);
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IRepository<WeighingRecordAttachment, int> _weighingRecordAttachmentRepository;

    // Rx Subject for weighing record creation events
    private readonly Subject<WeighingRecord> _weighingRecordCreatedSubject = new();
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;

    // Delivery type management using BehaviorSubject
    private readonly BehaviorSubject<DeliveryType> _deliveryTypeSubject = new(DeliveryType.Receiving);

    // Weight stability stream (shared, refcounted)
    private IObservable<bool>? _weightStabilityStream;

    // Last created weighing record ID stream
    private readonly Subject<long> _lastCreatedWeighingRecordIdSubject = new();

    // 订阅管理
    private IDisposable? _weightSubscription;


    /// <summary>
    ///     启动监听
    /// </summary>
    public async Task StartAsync()
    {
        // Load configuration from settings
        await LoadConfigurationAsync();

        if (_weightSubscription != null) return; // 已经启动

        // 重置状态
        _statusSubject.OnNext(AttendedWeighingStatus.OffScale);
        _plateNumberCache.Clear();

        // 1. 重量流（更频繁，用于状态转换）
        var weightStream = _truckScaleWeightService.WeightUpdates
            .Buffer(TimeSpan.FromMilliseconds(_stabilityCheckIntervalMs)) // 200ms
            .Where(buffer => buffer.Count > 0)
            .Select(buffer => buffer.Last())
            .DistinctUntilChanged() // 只在重量变化时发出
            .StartWith(0m);

        // 2. 稳定性流（较慢，用于稳定性检查）
        var stabilityStream = _truckScaleWeightService.WeightUpdates
            .Buffer(TimeSpan.FromMilliseconds(_stabilityWindowMs),
                TimeSpan.FromMilliseconds(_stabilityCheckIntervalMs))
            .Select(buffer =>
            {
                if (buffer.Count > 0)
                {
                    var min = buffer.Min();
                    var max = buffer.Max();
                    var range = max - min;
                    var isStable = range <= _weightStabilityThreshold * 2;
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

        // Store stability-only stream for IsWeightStable property
        _weightStabilityStream = stabilityStream.Select(info => info.IsStable);

        // 3. 状态转换流（只依赖重量，使用 Scan 管理状态）
        // 从当前状态开始，而不是从 OffScale 开始，以保持与 _statusSubject 同步
        var statusStream = weightStream
            .Scan(_statusSubject.Value, (currentStatus, weight) =>
            {
                return currentStatus switch
                {
                    AttendedWeighingStatus.OffScale when weight > _minWeightThreshold
                        => AttendedWeighingStatus.WaitingForStability,
                    AttendedWeighingStatus.WaitingForStability when weight < _minWeightThreshold
                        => AttendedWeighingStatus.OffScale,
                    AttendedWeighingStatus.WeightStabilized when weight < _minWeightThreshold
                        => AttendedWeighingStatus.OffScale,
                    _ => currentStatus // No state change
                };
            })
            .DistinctUntilChanged();

        // 4. 合并流：状态转换 + 稳定性检查
        _weightSubscription = statusStream.CombineLatest(weightStream,
                stabilityStream,
                (status, weight, stability) => new
                {
                    Status = status,
                    Weight = weight,
                    Stability = stability
                })
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(
                data => OnWeightAndStatusChanged(data.Status, data.Weight, data.Stability),
                error =>
                {
                    _logger?.LogError(error, "Error in weight updates subscription");
                });

        _logger?.LogInformation("Started monitoring truck scale weight changes");

        await Task.CompletedTask;
    }

    /// <summary>
    ///     停止监听
    /// </summary>
    public async Task StopAsync()
    {
        _weightSubscription?.Dispose();
        _weightSubscription = null;
        _weightStabilityStream = null; // Will be disposed by RefCount when no subscribers
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
    ///     Observable stream of status changes
    /// </summary>
    public IObservable<AttendedWeighingStatus> StatusChanges => _statusSubject;

    /// <summary>
    ///     Observable stream of most frequent plate number changes
    /// </summary>
    public IObservable<string?> MostFrequentPlateNumberChanges => _plateNumberSubject;

    /// <summary>
    ///     Check if weight is stable (changes less than ±0.1m within 3 seconds)
    /// </summary>
    public bool IsWeightStable
    {
        get
        {
            if (_weightStabilityStream == null) return false;
            
            // Synchronously get latest value from stream
            bool latestValue = false;
            using (var subscription = _weightStabilityStream
                .Take(1)
                .Subscribe(value => latestValue = value))
            {
                // Value is captured in subscription
            }
            return latestValue;
        }
    }

    /// <summary>
    ///     Observable stream of new weighing record creation events
    /// </summary>
    public IObservable<WeighingRecord> WeighingRecordCreated => _weighingRecordCreatedSubject;

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
        }
    }

    /// <summary>
    ///     Observable stream of delivery type changes
    /// </summary>
    public IObservable<DeliveryType> DeliveryTypeChanges => _deliveryTypeSubject;

    /// <summary>
    ///     接收车牌识别结果
    /// </summary>
    public void OnPlateNumberRecognized(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber)) return;

        var currentStatus = _statusSubject.Value;
        
        // Cache plate number recognition results only during WaitingForStability and WeightStabilized states
        if (currentStatus == AttendedWeighingStatus.WaitingForStability ||
            currentStatus == AttendedWeighingStatus.WeightStabilized)
        {
            _plateNumberCache.AddOrUpdate(
                plateNumber,
                new PlateNumberCacheRecord { Count = 1, LastUpdateTime = DateTime.UtcNow },
                (key, oldValue) => new PlateNumberCacheRecord
                    { Count = oldValue.Count + 1, LastUpdateTime = DateTime.UtcNow });
            _logger?.LogDebug(
                $"Cached plate number recognition result: {plateNumber} (count: {_plateNumberCache[plateNumber].Count})");

            // Notify observers of plate number update
            var mostFrequent = GetMostFrequentPlateNumber();
            _plateNumberSubject.OnNext(mostFrequent);
        }
    }

    /// <summary>
    ///     获取当前识别次数最大的车牌号
    /// </summary>
    public string? GetMostFrequentPlateNumber()
    {
        if (_plateNumberCache.IsEmpty) return null;

        var mostFrequent = _plateNumberCache
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
        
        // Safely complete and dispose subjects
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
            _plateNumberSubject?.OnCompleted();
        }
        catch (InvalidOperationException)
        {
            // Subject already in error or completed state, ignore
        }
        finally
        {
            _plateNumberSubject?.Dispose();
        }

        try
        {
            _weighingRecordCreatedSubject?.OnCompleted();
        }
        catch (InvalidOperationException)
        {
            // Subject already in error or completed state, ignore
        }
        finally
        {
            _weighingRecordCreatedSubject?.Dispose();
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
    ///     Load configuration from settings
    /// </summary>
    private async Task LoadConfigurationAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            var config = settings.WeighingConfiguration;

            _minWeightThreshold = config.MinWeightThreshold;
            _weightStabilityThreshold = config.WeightStabilityThreshold;
            _stabilityWindowMs = config.StabilityWindowMs;
            _stabilityCheckIntervalMs = config.StabilityCheckIntervalMs;

            _logger?.LogInformation(
                $"Loaded configuration - MinWeightThreshold: {_minWeightThreshold}, WeightStabilityThreshold: {_weightStabilityThreshold}, StabilityWindowMs: {_stabilityWindowMs}, StabilityCheckIntervalMs: {_stabilityCheckIntervalMs}");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "Failed to load configuration, using default values");
        }
    }


    /// <summary>
    ///     重量和状态变化处理（解耦后的处理）
    /// </summary>
    private void OnWeightAndStatusChanged(AttendedWeighingStatus newStatus, decimal weight, WeightStabilityInfo stability)
    {
        var previousStatus = _statusSubject.Value;

        // 处理状态转换（基于重量）
        if (newStatus != previousStatus)
        {
            _logger?.LogInformation(
                $"Status changed {previousStatus} -> {newStatus}, current weight: {weight}t");

            // 处理状态转换的副作用
            ProcessStatusTransition(previousStatus, newStatus, weight);

            // Notify observers of status change
            _statusSubject.OnNext(newStatus);
        }

        // 处理稳定性触发的操作（基于稳定性检查）
        // 注意：这里检查的是 _statusSubject.Value 而不是 newStatus，因为状态转换流可能还没有更新
        var currentStatus = _statusSubject.Value;
        if (currentStatus == AttendedWeighingStatus.WaitingForStability && stability.IsStable)
        {
            // Weight stabilized - use stable weight (average) if available
            var weightToUse = stability.StableWeight ?? weight;
            _logger?.LogInformation(
                $"Weight stabilized, stable weight: {weightToUse}t");

            // When weight is stabilized, capture photos and create WeighingRecord
            _ = Task.Run(async () => await OnWeightStabilizedAsync(weightToUse));

            // Update status to WeightStabilized
            _statusSubject.OnNext(AttendedWeighingStatus.WeightStabilized);
        }
    }

    /// <summary>
    ///     处理状态转换的副作用（解耦后，状态转换已在流中完成）
    /// </summary>
    private void ProcessStatusTransition(AttendedWeighingStatus previousStatus, AttendedWeighingStatus newStatus, decimal weight)
    {
        switch (previousStatus, newStatus)
        {
            case (AttendedWeighingStatus.OffScale, AttendedWeighingStatus.WaitingForStability):
                // OffScale -> WaitingForStability: weight increases above threshold
                _logger.LogInformation(
                    $"Entered WaitingForStability state, weight: {weight}t");
                break;

            case (AttendedWeighingStatus.WaitingForStability, AttendedWeighingStatus.OffScale):
                // Unstable weighing flow: directly from WaitingForStability to OffScale
                _logger?.LogWarning(
                    $"Unstable weighing flow, weight returned to {weight}t, triggered capture");

                // Capture all cameras and log (no need to save photos)
                _ = Task.Run(async () =>
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
                _ = Task.Run(async () =>
                {
                    await TryReWritePlateNumberAsync();
                    ClearPlateNumberCache();
                });
                break;

            case (AttendedWeighingStatus.WeightStabilized, AttendedWeighingStatus.OffScale):
                // WeightStabilized -> OffScale: normal flow
                _logger?.LogInformation(
                    $"Normal flow completed, entered OffScale state, weight: {weight}t");

                // Try to rewrite plate number, then clear cache
                _ = Task.Run(async () =>
                {
                    await TryReWritePlateNumberAsync();
                    ClearPlateNumberCache();
                });
                break;
        }
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
            var weighingRecord = new WeighingRecord(weight, plateNumber);
            weighingRecord.DeliveryType = _deliveryTypeSubject.Value;
            await _weighingRecordRepository.InsertAsync(weighingRecord);
            await uow.CompleteAsync();

            _logger?.LogInformation(
                $"Created weighing record successfully, ID: {weighingRecord.Id}, Weight: {weight}t, PlateNumber: {plateNumber ?? "None"}, DeliveryType: {_deliveryTypeSubject.Value}");

            // 保存最近创建的称重记录ID，用于后续重写车牌号（通过 Subject 传递）
            _lastCreatedWeighingRecordIdSubject.OnNext(weighingRecord.Id);

            // Notify observers that a new weighing record was created
            _weighingRecordCreatedSubject.OnNext(weighingRecord);

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
        long? recordId = null;
        
        // Get latest record ID from stream (take one and dispose immediately)
        using (var subscription = _lastCreatedWeighingRecordIdSubject
            .Take(1)
            .Subscribe(id => recordId = id))
        {
            // Value captured in subscription
        }

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
        _plateNumberCache.Clear();
        _logger?.LogDebug("Cleared plate number cache");

        // Notify observers that plate number is cleared
        _plateNumberSubject.OnNext(null);
    }
}