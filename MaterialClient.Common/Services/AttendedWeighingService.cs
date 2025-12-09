using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
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
}

/// <summary>
/// 有人值守称重服务
/// 监听地磅重量变化，管理称重状态，处理车牌识别缓存，并在适当时机进行抓拍和创建称重记录
/// </summary>
[AutoConstructor]
public partial class AttendedWeighingService : DomainService, IAttendedWeighingService
{
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly IHikvisionService _hikvisionService;
    private readonly ISettingsService _settingsService;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<WeighingRecordAttachment, int> _weighingRecordAttachmentRepository;
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<AttendedWeighingService> _logger;

    // Status management
    private AttendedWeighingStatus _currentStatus = AttendedWeighingStatus.OffScale;
    private readonly Lock _statusLock = new Lock();

    // Rx Subject for status updates
    private readonly Subject<AttendedWeighingStatus> _statusSubject = new();

    // Rx Subject for plate number updates
    private readonly Subject<string?> _plateNumberSubject = new();

    // 重量稳定判定
    private const decimal WeightThreshold = 0.5m; // 0.5t = 500kg
    private const decimal WeightStabilityTolerance = 0.1m; // 0.1t = 100kg
    private const int StabilityDurationMs = 10000; // 10秒

    private decimal? _stableWeight = null; // 进入稳定状态时的重量值

    // 重量历史记录
    private readonly ConcurrentBag<WeightHistoryRecord> _weightHistory = new ConcurrentBag<WeightHistoryRecord>();

    // 车牌识别缓存
    private readonly ConcurrentDictionary<string, PlateNumberCacheRecord> _plateNumberCache = new();

    private string? _selectedPlateNumber = null;

    // 订阅管理
    private IDisposable? _weightSubscription;


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
            // 清空重量历史记录（ConcurrentBag不支持Clear，需要重新创建）
            while (_weightHistory.TryTake(out _))
            {
            }

            _plateNumberCache.Clear();
            _selectedPlateNumber = null;

            _weightSubscription = _truckScaleWeightService.WeightUpdates
                .Subscribe(OnWeightChanged);

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
    /// 重量变化处理
    /// </summary>
    private void OnWeightChanged(decimal weight)
    {
        var now = DateTime.UtcNow;

        // 添加重量到历史记录
        AddWeightToHistory(weight, now);

        lock (_statusLock)
        {
            var previousStatus = _currentStatus;
            ProcessWeightChange(weight);

            // Log status changes and notify observers
            if (_currentStatus != previousStatus)
            {
                _logger?.LogInformation(
                    $"AttendedWeighingService: Status changed {previousStatus} -> {_currentStatus}, current weight: {weight}kg");

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
                if (currentWeight > WeightThreshold)
                {
                    _currentStatus = AttendedWeighingStatus.WaitingForStability;
                    _statusSubject.OnNext(_currentStatus);
                    _stableWeight = null;

                    // Select plate number (most frequent from cache)
                    SelectPlateNumberFromCache();

                    _logger.LogInformation(
                        $"AttendedWeighingService: Entered WaitingForStability state, weight: {currentWeight}kg");
                }

                break;

            case AttendedWeighingStatus.WaitingForStability:
                if (currentWeight < WeightThreshold)
                {
                    // Unstable weighing flow: directly from WaitingForStability to OffScale
                    _currentStatus = AttendedWeighingStatus.OffScale;
                    _statusSubject.OnNext(_currentStatus);
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

                    // Clear plate number cache
                    ClearPlateNumberCache();

                    _logger?.LogWarning(
                        $"AttendedWeighingService: Unstable weighing flow, weight returned to {currentWeight}kg, triggered capture");
                }
                else
                {
                    // Check if weight is stable
                    CheckWeightStability(currentWeight);
                }

                break;

            case AttendedWeighingStatus.WeightStabilized:
                if (currentWeight < WeightThreshold)
                {
                    // WeightStabilized -> OffScale: normal flow
                    _currentStatus = AttendedWeighingStatus.OffScale;
                    _statusSubject.OnNext(_currentStatus);

                    // Check again if there are more frequent plate numbers, update if needed
                    UpdatePlateNumberIfNeeded();

                    // Clear plate number cache
                    ClearPlateNumberCache();

                    _logger?.LogInformation(
                        $"AttendedWeighingService: Normal flow completed, entered OffScale state, weight: {currentWeight}kg");
                }

                break;
        }
    }

    /// <summary>
    /// 检查重量稳定性
    /// </summary>
    private void CheckWeightStability(decimal currentWeight)
    {
        // 使用历史记录判断重量是否稳定
        bool isStable = IsWeightStable(StabilityDurationMs, WeightStabilityTolerance);

        if (isStable)
        {
            // 进入稳定状态
            if (_stableWeight == null)
            {
                // 从历史记录获取最新的重量值
                var latestWeight = GetLatestWeightFromHistory();
                if (latestWeight.HasValue)
                {
                    _stableWeight = latestWeight.Value;
                }
                else
                {
                    _stableWeight = currentWeight;
                }

                // Select plate number (most frequent from cache)
                SelectPlateNumberFromCache();

                // State transition: WaitingForStability -> WeightStabilized
                _currentStatus = AttendedWeighingStatus.WeightStabilized;

                _logger?.LogInformation(
                    $"AttendedWeighingService: Weight stabilized, stable weight: {_stableWeight}kg");

                // When weight is stabilized, capture photos and create WeighingRecord
                _ = Task.Run(async () => await OnWeightStabilizedAsync());
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
    private async Task OnWeightStabilizedAsync()
    {
        try
        {
            // Capture all cameras
            var photoPaths = await CaptureAllCamerasAsync("WeightStabilized");

            // 创建WeighingRecord（传入照片路径）
            await CreateWeighingRecordAsync(photoPaths);
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

            if (cameraConfigs == null || cameraConfigs.Count == 0)
            {
                _logger?.LogWarning($"AttendedWeighingService: No cameras configured, cannot capture ({reason})");
                return new List<string>();
            }

            // 转换为 BatchCaptureRequest
            var requests = new List<BatchCaptureRequest>();
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var basePath = Path.Combine(AppContext.BaseDirectory, "Photos", timestamp);

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

                var fileName = $"{cameraConfig.Name}_{channel}_{Guid.NewGuid():N}.jpg";
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
    private async Task CreateWeighingRecordAsync(List<string> photoPaths)
    {
        try
        {
            decimal weight;
            string? plateNumber;

            lock (_statusLock)
            {
                // 如果处于稳定状态，从历史记录获取最新的重量
                if (_currentStatus == AttendedWeighingStatus.WeightStabilized)
                {
                    var latestWeight = GetLatestWeightFromHistory();
                    if (latestWeight.HasValue)
                    {
                        weight = latestWeight.Value;
                    }
                    else if (_stableWeight != null)
                    {
                        // 如果历史记录中没有，使用记录的稳定重量
                        weight = _stableWeight.Value;
                    }
                    else
                    {
                        _logger?.LogError(
                            "AttendedWeighingService: Cannot create WeighingRecord, stable weight value is null");
                        return;
                    }
                }
                else if (_stableWeight != null)
                {
                    // 使用记录的稳定重量
                    weight = _stableWeight.Value;
                }
                else
                {
                    _logger?.LogError(
                        "AttendedWeighingService: Cannot create WeighingRecord, stable weight value is null");
                    return;
                }

                plateNumber = _selectedPlateNumber;
            }

            using var uow = _unitOfWorkManager.Begin();

            // Create weighing record
            var weighingRecord = new WeighingRecord(weight)
            {
                PlateNumber = plateNumber
            };

            await _weighingRecordRepository.InsertAsync(weighingRecord);
            await uow.CompleteAsync();

            _logger?.LogInformation(
                $"AttendedWeighingService: Created weighing record successfully, ID: {weighingRecord.Id}, Weight: {weight}kg, PlateNumber: {plateNumber ?? "None"}");

            // Save captured photos to WeighingRecordAttachment
            if (photoPaths != null && photoPaths.Count > 0)
            {
                await SaveCapturePhotosAsync(weighingRecord.Id, photoPaths);
            }
            else
            {
                _logger?.LogWarning(
                    $"AttendedWeighingService: Weighing record {weighingRecord.Id} has no associated photos");
            }
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
                    var attachmentFile = new AttachmentFile(fileName, photoPath, AttachType.EntryPhoto);

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
    /// 从缓存中选择车牌（识别次数最多的）
    /// </summary>
    private void SelectPlateNumberFromCache()
    {
        if (_plateNumberCache.IsEmpty)
        {
            _selectedPlateNumber = null;
            return;
        }

        var mostFrequent = _plateNumberCache
            .OrderByDescending(kvp => kvp.Value.Count)
            .FirstOrDefault();

        _selectedPlateNumber = mostFrequent.Key;
        _logger?.LogInformation(
            $"AttendedWeighingService: Selected plate number: {_selectedPlateNumber} (recognition count: {mostFrequent.Value?.Count ?? 0})");

        // Notify observers of plate number update
        // _plateNumberSubject.OnNext(_selectedPlateNumber);
    }

    /// <summary>
    /// 更新车牌（如果缓存中有更多次数的车牌）
    /// </summary>
    private void UpdatePlateNumberIfNeeded()
    {
        if (_plateNumberCache.IsEmpty)
        {
            return;
        }

        var mostFrequent = _plateNumberCache
            .OrderByDescending(kvp => kvp.Value.Count)
            .FirstOrDefault();

        var currentCount = 0;
        if (!string.IsNullOrEmpty(_selectedPlateNumber) &&
            _plateNumberCache.TryGetValue(_selectedPlateNumber, out var currentRecord))
        {
            currentCount = currentRecord.Count;
        }

        if (!string.IsNullOrEmpty(mostFrequent.Key) &&
            (string.IsNullOrEmpty(_selectedPlateNumber) ||
             (mostFrequent.Value?.Count ?? 0) > currentCount))
        {
            _selectedPlateNumber = mostFrequent.Key;
            _logger?.LogInformation(
                $"AttendedWeighingService: Updated plate number: {_selectedPlateNumber} (recognition count: {mostFrequent.Value?.Count ?? 0})");

            // Notify observers of plate number update
            // _plateNumberSubject.OnNext(_selectedPlateNumber);
        }
    }

    /// <summary>
    /// 清空车牌缓存
    /// </summary>
    private void ClearPlateNumberCache()
    {
        _plateNumberCache.Clear();
        _selectedPlateNumber = null;
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
    /// 判断重量是否稳定
    /// </summary>
    /// <param name="stabilityDelayMs">稳定延迟时间（毫秒）</param>
    /// <param name="stabilityThreshold">稳定阈值</param>
    /// <returns>是否稳定</returns>
    private bool IsWeightStable(int stabilityDelayMs, decimal stabilityThreshold)
    {
        var now = DateTime.UtcNow;
        var cutoffTime = now.AddMilliseconds(-stabilityDelayMs);

        // 获取时间窗口内的所有重量记录（ConcurrentBag是线程安全的，可以直接查询）
        var recentWeights = _weightHistory
            .Where(x => x.Time >= cutoffTime)
            .Select(x => x.Weight)
            .ToList();

        int expectedCount = stabilityDelayMs / 300; // 理论记录数（假设每300ms一条记录）
        int minRequiredCount = (int)(expectedCount * 0.8); // 至少需要80%的记录

        if (recentWeights.Count < minRequiredCount)
        {
            return false; // 时间窗口内记录不足
        }

        // 计算时间窗口内的最大和最小重量
        var maxWeight = recentWeights.Max();
        var minWeight = recentWeights.Min();
        var weightRange = maxWeight - minWeight;

        // 如果变化绝对值小于阈值，则认为稳定
        return Math.Abs(weightRange) < stabilityThreshold;
    }

    /// <summary>
    /// 添加重量到历史记录
    /// </summary>
    /// <param name="weight">重量值</param>
    /// <param name="time">时间</param>
    private void AddWeightToHistory(decimal weight, DateTime time)
    {
        _weightHistory.Add(new WeightHistoryRecord
        {
            Time = time,
            Weight = weight
        });

        // 只保留最近5秒内的记录（多保留一些，避免频繁清理）
        // ConcurrentBag不支持RemoveAll，需要定期清理旧记录
        var cutoffTime = time.AddMilliseconds(-5000);
        CleanOldRecords(cutoffTime);
    }

    /// <summary>
    /// 清理旧记录
    /// </summary>
    /// <param name="cutoffTime">截止时间</param>
    private void CleanOldRecords(DateTime cutoffTime)
    {
        // 由于ConcurrentBag不支持直接删除，我们需要：
        // 1. 获取所有记录的快照
        // 2. 过滤出需要保留的记录
        // 3. 清空并重新添加

        // 为了避免频繁清理，只在记录数量较多时进行清理
        if (_weightHistory.Count < 50)
        {
            return;
        }

        var recordsToKeep = _weightHistory
            .Where(x => x.Time >= cutoffTime)
            .ToList();

        // 如果大部分记录都需要保留，就不清理了
        if (recordsToKeep.Count > _weightHistory.Count * 0.8)
        {
            return;
        }

        // 清空并重新添加需要保留的记录
        while (_weightHistory.TryTake(out _))
        {
        }

        foreach (var record in recordsToKeep)
        {
            _weightHistory.Add(record);
        }
    }

    /// <summary>
    /// 从历史记录获取最新的重量
    /// </summary>
    /// <returns>最新的重量值，如果没有记录则返回null</returns>
    private decimal? GetLatestWeightFromHistory()
    {
        if (_weightHistory.IsEmpty)
        {
            return null;
        }

        // 返回最新的重量记录（ConcurrentBag是线程安全的，可以直接查询）
        var latestRecord = _weightHistory.OrderByDescending(x => x.Time).FirstOrDefault();
        return latestRecord?.Weight;
    }

    /// <summary>
    /// 重量历史记录
    /// </summary>
    private class WeightHistoryRecord
    {
        public DateTime Time { get; set; }
        public decimal Weight { get; set; }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _statusSubject?.OnCompleted();
        _statusSubject?.Dispose();
        _plateNumberSubject?.OnCompleted();
        _plateNumberSubject?.Dispose();
    }
}