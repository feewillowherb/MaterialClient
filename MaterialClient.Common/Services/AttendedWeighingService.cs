using System;
using System.Collections.Concurrent;
using System.Linq;
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
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

/// <summary>
/// 车牌缓存记录
/// </summary>
public record PlateNumberCacheRecord
{
    /// <summary>
    /// 识别次数
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdateTime { get; init; }
}

/// <summary>
/// 有人值守称重服务接口
/// </summary>
public interface IAttendedWeighingService : IAsyncDisposable
{
    /// <summary>
    /// 启动监听
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// 停止监听
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 获取当前状态
    /// </summary>
    AttendedWeighingStatus GetCurrentStatus();

    /// <summary>
    /// 接收车牌识别结果
    /// </summary>
    void OnPlateNumberRecognized(string plateNumber);

    /// <summary>
    /// 获取当前识别次数最大的车牌号
    /// </summary>
    string? GetMostFrequentPlateNumber();

    /// <summary>
    /// Observable stream of status changes
    /// </summary>
    IObservable<AttendedWeighingStatus> StatusChanges { get; }

    /// <summary>
    /// Observable stream of most frequent plate number changes
    /// </summary>
    IObservable<string?> MostFrequentPlateNumberChanges { get; }

    /// <summary>
    /// Check if weight is stable (changes less than ±0.1m within 3 seconds)
    /// </summary>
    bool IsWeightStable { get; }

    /// <summary>
    /// Observable stream of new weighing record creation events
    /// </summary>
    IObservable<WeighingRecord> WeighingRecordCreated { get; }

    /// <summary>
    /// 获取当前收发料类型
    /// </summary>
    DeliveryType CurrentDeliveryType { get; }

    /// <summary>
    /// 设置收发料类型
    /// </summary>
    void SetDeliveryType(DeliveryType deliveryType);

    /// <summary>
    /// Observable stream of delivery type changes
    /// </summary>
    IObservable<DeliveryType> DeliveryTypeChanges { get; }
}

/// <summary>
/// 有人值守称重服务
/// 监听地磅重量变化，管理称重状态，处理车牌识别缓存，并在适当时机进行抓拍和创建称重记录
/// </summary>
[AutoConstructor]
public partial class AttendedWeighingService : IAttendedWeighingService, ISingletonDependency
{
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly IHikvisionService _hikvisionService;
    private readonly ISettingsService _settingsService;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<WeighingRecordAttachment, int> _weighingRecordAttachmentRepository;
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<AttendedWeighingService> _logger;

    // Status management
    private AttendedWeighingStatus _currentStatus = AttendedWeighingStatus.OffScale;
    private readonly Lock _statusLock = new Lock();

    // Rx Subject for status updates
    private readonly Subject<AttendedWeighingStatus> _statusSubject = new();

    // Rx Subject for plate number updates
    private readonly Subject<string?> _plateNumberSubject = new();

    // Rx Subject for weighing record creation events
    private readonly Subject<WeighingRecord> _weighingRecordCreatedSubject = new();

    // Rx Subject for delivery type changes
    private readonly Subject<DeliveryType> _deliveryTypeSubject = new();

    // 最近创建的称重记录ID，用于重写车牌号
    private long? _lastCreatedWeighingRecordId = null;

    // 当前收发料类型（默认为收料）
    private DeliveryType _currentDeliveryType = DeliveryType.Receiving;

    // 最小称重重量稳定判定
    private const decimal MinWeightThreshold = 0.5m; // 0.5t = 500kg

    private decimal? _stableWeight = null; // 进入稳定状态时的重量值

    // 车牌识别缓存
    private readonly ConcurrentDictionary<string, PlateNumberCacheRecord> _plateNumberCache = new();

    // 订阅管理
    private IDisposable? _weightSubscription;
    private IDisposable? _stabilitySubscription;

    // 重量稳定性监控
    private const decimal WeightStabilityThreshold = 0.05m; // ±0.05m = 0.1m total range
    private const int StabilityWindowMs = 3000;
    private bool _isWeightStable = false;
    private const int StabilityCheckIntervalMs = 200; // 默认 200ms


    /// <summary>
    /// 启动监听
    /// </summary>
    public async Task StartAsync()
    {
        lock (_statusLock)
        {
            if (_weightSubscription != null)
            {
                return; // 已经启动
            }

            // 重置状态
            _currentStatus = AttendedWeighingStatus.OffScale;
            _stableWeight = null;

            _plateNumberCache.Clear();
            _weightSubscription = _truckScaleWeightService.WeightUpdates
                .Buffer(TimeSpan.FromMilliseconds(200)) // 收集200ms内的数据
                .ObserveOn(TaskPoolScheduler.Default)
                .Subscribe(buffer =>
                {
                    if (buffer.Count > 0)
                    {
                        OnWeightChanged(buffer.Last()); // 只处理最新的
                    }
                });

            // Initialize weight stability monitoring
            InitializeWeightStabilityMonitoring();

            _logger?.LogInformation("AttendedWeighingService: Started monitoring truck scale weight changes");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 停止监听
    /// </summary>
    public async Task StopAsync()
    {
        lock (_statusLock)
        {
            _weightSubscription?.Dispose();
            _weightSubscription = null;
            _stabilitySubscription?.Dispose();
            _stabilitySubscription = null;
            _isWeightStable = false;
            _logger?.LogInformation("AttendedWeighingService: Stopped monitoring truck scale weight changes");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 获取当前状态
    /// </summary>
    public AttendedWeighingStatus GetCurrentStatus()
    {
        lock (_statusLock)
        {
            return _currentStatus;
        }
    }

    /// <summary>
    /// Observable stream of status changes
    /// </summary>
    public IObservable<AttendedWeighingStatus> StatusChanges => _statusSubject;

    /// <summary>
    /// Observable stream of most frequent plate number changes
    /// </summary>
    public IObservable<string?> MostFrequentPlateNumberChanges => _plateNumberSubject;

    /// <summary>
    /// Check if weight is stable (changes less than ±0.1m within 3 seconds)
    /// </summary>
    public bool IsWeightStable
    {
        get
        {
            lock (_statusLock)
            {
                return _isWeightStable;
            }
        }
    }

    /// <summary>
    /// Observable stream of new weighing record creation events
    /// </summary>
    public IObservable<WeighingRecord> WeighingRecordCreated => _weighingRecordCreatedSubject;

    /// <summary>
    /// 获取当前收发料类型
    /// </summary>
    public DeliveryType CurrentDeliveryType
    {
        get
        {
            lock (_statusLock)
            {
                return _currentDeliveryType;
            }
        }
    }

    /// <summary>
    /// 设置收发料类型
    /// </summary>
    public void SetDeliveryType(DeliveryType deliveryType)
    {
        lock (_statusLock)
        {
            if (_currentDeliveryType != deliveryType)
            {
                _currentDeliveryType = deliveryType;
                _deliveryTypeSubject.OnNext(deliveryType);
                _logger?.LogInformation($"AttendedWeighingService: DeliveryType changed to {deliveryType}");
            }
        }
    }

    /// <summary>
    /// Observable stream of delivery type changes
    /// </summary>
    public IObservable<DeliveryType> DeliveryTypeChanges => _deliveryTypeSubject;

    /// <summary>
    /// 接收车牌识别结果
    /// </summary>
    public void OnPlateNumberRecognized(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
        {
            return;
        }

        lock (_statusLock)
        {
            // Cache plate number recognition results only during WaitingForStability and WeightStabilized states
            if (_currentStatus == AttendedWeighingStatus.WaitingForStability ||
                _currentStatus == AttendedWeighingStatus.WeightStabilized)
            {
                _plateNumberCache.AddOrUpdate(
                    plateNumber,
                    new PlateNumberCacheRecord { Count = 1, LastUpdateTime = DateTime.UtcNow },
                    (key, oldValue) => new PlateNumberCacheRecord
                        { Count = oldValue.Count + 1, LastUpdateTime = DateTime.UtcNow });
                _logger?.LogDebug(
                    $"AttendedWeighingService: Cached plate number recognition result: {plateNumber} (count: {_plateNumberCache[plateNumber].Count})");

                // Notify observers of plate number update
                var mostFrequent = GetMostFrequentPlateNumber();
                _plateNumberSubject.OnNext(mostFrequent);
            }
        }
    }

    /// <summary>
    /// Initialize weight stability monitoring using Rx
    /// Monitors weight changes within 3 seconds window, considers stable if variation is less than ±0.1m
    /// </summary>
    private void InitializeWeightStabilityMonitoring()
    {
        _stabilitySubscription?.Dispose();

        _stabilitySubscription = _truckScaleWeightService.WeightUpdates
            .Buffer(TimeSpan.FromMilliseconds(StabilityWindowMs),
                TimeSpan.FromMilliseconds(StabilityCheckIntervalMs))
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(buffer =>
            {
                if (buffer.Count > 0)
                {
                    var min = buffer.Min();
                    var max = buffer.Max();
                    var range = max - min;

                    // Consider stable if range is within ±0.1m (0.2m total range)
                    bool isStable = range <= WeightStabilityThreshold * 2;

                    lock (_statusLock)
                    {
                        _isWeightStable = isStable;
                    }

                    _logger?.LogDebug(
                        $"Weight stability: {isStable} (range: {range:F3} kg, min: {min:F3}, max: {max:F3})");
                }
                else
                {
                    // No data in buffer, consider unstable
                    lock (_statusLock)
                    {
                        _isWeightStable = false;
                    }
                }
            });
    }

    /// <summary>
    /// 重量变化处理
    /// </summary>
    private void OnWeightChanged(decimal weight)
    {
        lock (_statusLock)
        {
            var previousStatus = _currentStatus;
            ProcessWeightChange(weight);

            // Log status changes and notify observers
            if (_currentStatus != previousStatus)
            {
                _logger?.LogInformation(
                    $"AttendedWeighingService: Status changed {previousStatus} -> {_currentStatus}, current weight: {weight}t");

                // Notify observers of status change
                _statusSubject.OnNext(_currentStatus);
            }
        }
    }

    /// <summary>
    /// 处理重量变化
    /// </summary>
    private void ProcessWeightChange(decimal currentWeight)
    {
        switch (_currentStatus)
        {
            case AttendedWeighingStatus.OffScale:
                // OffScale -> WaitingForStability: weight increases from <0.5t to >0.5t
                if (currentWeight > MinWeightThreshold)
                {
                    _currentStatus = AttendedWeighingStatus.WaitingForStability;
                    _stableWeight = null;

                    _logger.LogInformation(
                        $"AttendedWeighingService: Entered WaitingForStability state, weight: {currentWeight}t");
                }

                break;

            case AttendedWeighingStatus.WaitingForStability:
                if (currentWeight < MinWeightThreshold)
                {
                    // Unstable weighing flow: directly from WaitingForStability to OffScale
                    _currentStatus = AttendedWeighingStatus.OffScale;
                    _stableWeight = null;

                    // Capture all cameras and log (no need to save photos)
                    _ = Task.Run(async () =>
                    {
                        var photos = await CaptureAllCamerasAsync("UnstableWeighingFlow");
                        if (photos.Count == 0)
                        {
                            _logger?.LogWarning(
                                $"AttendedWeighingService: Unstable weighing flow capture completed, but no photos were obtained");
                        }
                        else
                        {
                            _logger?.LogInformation(
                                $"AttendedWeighingService: Unstable weighing flow captured {photos.Count} photos");
                        }
                    });

                    // Try to rewrite plate number, then clear cache
                    _ = Task.Run(async () =>
                    {
                        await TryReWritePlateNumberAsync();
                        ClearPlateNumberCache();
                    });

                    _logger?.LogWarning(
                        $"AttendedWeighingService: Unstable weighing flow, weight returned to {currentWeight}t, triggered capture");
                }
                else
                {
                    // Check if weight is stable
                    CheckWeightStability(currentWeight);
                }

                break;

            case AttendedWeighingStatus.WeightStabilized:
                if (currentWeight < MinWeightThreshold)
                {
                    // WeightStabilized -> OffScale: normal flow
                    _currentStatus = AttendedWeighingStatus.OffScale;

                    // Try to rewrite plate number, then clear cache
                    _ = Task.Run(async () =>
                    {
                        await TryReWritePlateNumberAsync();
                        ClearPlateNumberCache();
                    });

                    _logger?.LogInformation(
                        $"AttendedWeighingService: Normal flow completed, entered OffScale state, weight: {currentWeight}t");
                }

                break;
        }
    }

    /// <summary>
    /// 检查重量稳定性
    /// </summary>
    private void CheckWeightStability(decimal currentWeight)
    {
        if (IsWeightStable)
        {
            // 进入稳定状态
            if (_stableWeight == null)
            {
                _stableWeight = currentWeight;

                // State transition: WaitingForStability -> WeightStabilized
                _currentStatus = AttendedWeighingStatus.WeightStabilized;

                _logger?.LogInformation(
                    $"AttendedWeighingService: Weight stabilized, stable weight: {_stableWeight}t");

                // When weight is stabilized, capture photos and create WeighingRecord
                _ = Task.Run(async () => await OnWeightStabilizedAsync(currentWeight));
            }
        }
        else
        {
            // 不稳定，重置稳定状态
            _stableWeight = null;
        }
    }

    /// <summary>
    /// 重量已稳定时的处理
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
            _logger?.LogError(ex, "AttendedWeighingService: Error occurred while processing weight stabilization");
        }
    }

    /// <summary>
    /// 抓拍所有相机
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
                _logger?.LogWarning($"AttendedWeighingService: No cameras configured, cannot capture ({reason})");
                return new List<string>();
            }

            // 转换为 BatchCaptureRequest
            var requests = new List<BatchCaptureRequest>();
            var now = DateTime.Now;
            var basePath = AttachmentPathUtils.GetLocalStoragePath(AttachType.EntryPhoto, now);

            foreach (var cameraConfig in cameraConfigs)
            {
                if (string.IsNullOrWhiteSpace(cameraConfig.Ip) ||
                    string.IsNullOrWhiteSpace(cameraConfig.Port) ||
                    string.IsNullOrWhiteSpace(cameraConfig.Channel))
                {
                    continue;
                }

                if (!int.TryParse(cameraConfig.Port, out var port) ||
                    !int.TryParse(cameraConfig.Channel, out var channel))
                {
                    _logger?.LogWarning($"AttendedWeighingService: Invalid camera configuration: {cameraConfig.Name}");
                    continue;
                }

                var hikvisionConfig = new HikvisionDeviceConfig
                {
                    Ip = cameraConfig.Ip,
                    Port = port,
                    Username = cameraConfig.UserName,
                    Password = cameraConfig.Password,
                    Channels = new[] { channel }
                };

                var fileName = AttachmentPathUtils.GenerateMonitoringPhotoFileName(cameraConfig.Name, channel);
                var savePath = Path.Combine(basePath, fileName);

                requests.Add(new BatchCaptureRequest
                {
                    Config = hikvisionConfig,
                    Channel = channel,
                    SaveFullPath = savePath,
                    DeviceKey = $"{cameraConfig.Ip}:{port}"
                });
            }

            if (requests.Count == 0)
            {
                _logger?.LogWarning(
                    $"AttendedWeighingService: No valid camera configurations, cannot capture ({reason})");
                return new List<string>();
            }

            _logger?.LogInformation(
                $"AttendedWeighingService: Starting capture for {requests.Count} cameras ({reason})");

            var results = await _hikvisionService.CaptureJpegFromStreamBatchAsync(requests);

            var successCount = results.Count(r => r.Success);
            var failCount = results.Count - successCount;

            _logger?.LogInformation(
                $"AttendedWeighingService: Capture completed, success: {successCount}, failed: {failCount} ({reason})");

            // Log detailed failure information
            foreach (var result in results.Where(r => !r.Success))
            {
                _logger?.LogWarning(
                    $"AttendedWeighingService: Capture failed - Device: {result.Request.DeviceKey}, Channel: {result.Request.Channel}, Error: {result.ErrorMessage}");
            }

            // Return list of successfully captured photo paths
            var photoPaths = results.Where(r => r.Success && File.Exists(r.Request.SaveFullPath))
                .Select(r => r.Request.SaveFullPath)
                .ToList();

            // Log if photo list is empty
            if (photoPaths.Count == 0)
            {
                _logger?.LogWarning(
                    $"AttendedWeighingService: Capture completed, but no photos were successfully obtained ({reason})");
            }

            return photoPaths;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"AttendedWeighingService: Error occurred while capturing all cameras ({reason})");
            _logger?.LogWarning($"AttendedWeighingService: Capture exception, returning empty photo list ({reason})");
            return new List<string>();
        }
    }

    /// <summary>
    /// 创建称重记录
    /// </summary>
    private async Task CreateWeighingRecordAsync(decimal weight, List<string> photoPaths)
    {
        try
        {
            var plateNumber = GetMostFrequentPlateNumber();

            using var uow = _unitOfWorkManager.Begin();

            // Create weighing record with current delivery type
            var weighingRecord = new WeighingRecord(weight, plateNumber);
            weighingRecord.DeliveryType = _currentDeliveryType;
            await _weighingRecordRepository.InsertAsync(weighingRecord);
            await uow.CompleteAsync();

            _logger?.LogInformation(
                $"AttendedWeighingService: Created weighing record successfully, ID: {weighingRecord.Id}, Weight: {weight}t, PlateNumber: {plateNumber ?? "None"}, DeliveryType: {_currentDeliveryType}");

            // 保存最近创建的称重记录ID，用于后续重写车牌号
            lock (_statusLock)
            {
                _lastCreatedWeighingRecordId = weighingRecord.Id;
            }

            // Notify observers that a new weighing record was created
            _weighingRecordCreatedSubject.OnNext(weighingRecord);

            // Publish TryMatchEvent for automatic matching

            // Save captured photos to WeighingRecordAttachment
            if (photoPaths.Count > 0)
            {
                await SaveCapturePhotosAsync(weighingRecord.Id, photoPaths);
            }
            else
            {
                _logger?.LogWarning(
                    $"AttendedWeighingService: Weighing record {weighingRecord.Id} has no associated photos");
            }

            await _localEventBus.PublishAsync(new TryMatchEvent(weighingRecord.Id));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "AttendedWeighingService: Error occurred while creating weighing record");
        }
    }

    /// <summary>
    /// 保存抓拍的照片
    /// </summary>
    private async Task SaveCapturePhotosAsync(long weighingRecordId, List<string> photoPaths)
    {
        try
        {
            if (photoPaths.Count == 0)
            {
                return;
            }

            using var uow = _unitOfWorkManager.Begin();

            foreach (var photoPath in photoPaths)
            {
                try
                {
                    if (!File.Exists(photoPath))
                    {
                        _logger?.LogWarning($"AttendedWeighingService: Photo file does not exist: {photoPath}");
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
                    _logger?.LogWarning(ex, $"AttendedWeighingService: Failed to save photo: {photoPath}");
                }
            }

            await uow.CompleteAsync();
            _logger?.LogInformation(
                $"AttendedWeighingService: Saved {photoPaths.Count} photos to weighing record {weighingRecordId}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"AttendedWeighingService: Error occurred while saving captured photos");
        }
    }

    /// <summary>
    /// 尝试重写称重记录的车牌号
    /// 在清空车牌缓存前调用，用最频繁识别的车牌号更新最近创建的称重记录
    /// </summary>
    private async Task TryReWritePlateNumberAsync()
    {
        long? recordId;
        lock (_statusLock)
        {
            recordId = _lastCreatedWeighingRecordId;
            _lastCreatedWeighingRecordId = null; // 立即清空，防止重复操作
        }

        try
        {
            if (recordId == null)
            {
                _logger?.LogDebug("AttendedWeighingService: No recent weighing record to rewrite plate number");
                return;
            }

            var plateNumber = GetMostFrequentPlateNumber();
            if (string.IsNullOrWhiteSpace(plateNumber))
            {
                _logger?.LogDebug("AttendedWeighingService: No plate number to rewrite");
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
                    $"AttendedWeighingService: Rewrote plate number for weighing record {weighingRecord.Id}, from '{oldPlateNumber ?? "None"}' to '{plateNumber}'");

                await _localEventBus.PublishAsync(new TryMatchEvent(weighingRecord.Id));
            }
            else
            {
                _logger?.LogDebug(
                    $"AttendedWeighingService: Plate number unchanged for weighing record {recordId.Value}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "AttendedWeighingService: Error occurred while rewriting plate number");
        }
    }

    /// <summary>
    /// 清空车牌缓存
    /// </summary>
    private void ClearPlateNumberCache()
    {
        _plateNumberCache.Clear();
        _logger?.LogDebug("AttendedWeighingService: Cleared plate number cache");

        // Notify observers that plate number is cleared
        _plateNumberSubject.OnNext(null);
    }

    /// <summary>
    /// 获取当前识别次数最大的车牌号
    /// </summary>
    public string? GetMostFrequentPlateNumber()
    {
        lock (_statusLock)
        {
            if (_plateNumberCache.IsEmpty)
            {
                return null;
            }

            var mostFrequent = _plateNumberCache
                .OrderByDescending(kvp => kvp.Value.Count)
                .FirstOrDefault();

            return mostFrequent.Key;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _stabilitySubscription?.Dispose();
        _statusSubject?.OnCompleted();
        _statusSubject?.Dispose();
        _plateNumberSubject?.OnCompleted();
        _plateNumberSubject?.Dispose();
        _weighingRecordCreatedSubject?.OnCompleted();
        _weighingRecordCreatedSubject?.Dispose();
    }
}