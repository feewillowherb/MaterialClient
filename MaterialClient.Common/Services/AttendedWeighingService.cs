using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using MaterialClient.Common.Configuration;
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
/// 有人值守称重服务
/// 监听地磅重量变化，管理称重状态，处理车牌识别缓存，并在适当时机进行抓拍和创建称重记录
/// </summary>
public class AttendedWeighingService : DomainService, IDisposable
{
    private readonly ITruckScaleWeightService _truckScaleWeightService;
    private readonly IHikvisionService _hikvisionService;
    private readonly ISettingsService _settingsService;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IRepository<WeighingRecordAttachment, int> _weighingRecordAttachmentRepository;
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<AttendedWeighingService>? _logger;

    // 状态管理
    private AttendedWeighingStatus _currentStatus = AttendedWeighingStatus.下称;
    private readonly object _statusLock = new object();

    // 重量稳定判定
    private const decimal WeightThreshold = 500m; // 0.5t = 500kg
    private const decimal WeightStabilityTolerance = 100m; // 0.1t = 100kg
    private const int StabilityDurationMs = 3000; // 3秒

    private decimal _lastWeight = 0m;
    private decimal? _stableWeight = null; // 进入稳定状态时的重量值
    private DateTime? _stabilityStartTime = null;
    private decimal? _stabilityBaseWeight = null; // 稳定判定的基准重量

    // 车牌识别缓存
    private readonly ConcurrentDictionary<string, int> _plateNumberCache = new();
    private string? _selectedPlateNumber = null;

    // 订阅管理
    private IDisposable? _weightSubscription;

    public AttendedWeighingService(
        ITruckScaleWeightService truckScaleWeightService,
        IHikvisionService hikvisionService,
        ISettingsService settingsService,
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IRepository<WeighingRecordAttachment, int> weighingRecordAttachmentRepository,
        IRepository<AttachmentFile, int> attachmentFileRepository,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<AttendedWeighingService>? logger = null)
    {
        _truckScaleWeightService = truckScaleWeightService;
        _hikvisionService = hikvisionService;
        _settingsService = settingsService;
        _weighingRecordRepository = weighingRecordRepository;
        _weighingRecordAttachmentRepository = weighingRecordAttachmentRepository;
        _attachmentFileRepository = attachmentFileRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    /// <summary>
    /// 启动监听
    /// </summary>
    public void Start()
    {
        lock (_statusLock)
        {
            if (_weightSubscription != null)
            {
                return; // 已经启动
            }

            _weightSubscription = _truckScaleWeightService.WeightUpdates
                .Subscribe(OnWeightChanged);

            _logger?.LogInformation("AttendedWeighingService: 已启动监听地磅重量变化");
        }
    }

    /// <summary>
    /// 停止监听
    /// </summary>
    public void Stop()
    {
        lock (_statusLock)
        {
            _weightSubscription?.Dispose();
            _weightSubscription = null;
            _logger?.LogInformation("AttendedWeighingService: 已停止监听地磅重量变化");
        }
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
            // 只在缓存期间（上称等待重量稳定、上称重量已稳定）缓存车牌识别结果
            if (_currentStatus == AttendedWeighingStatus.上称等待重量稳定 ||
                _currentStatus == AttendedWeighingStatus.上称重量已稳定)
            {
                _plateNumberCache.AddOrUpdate(plateNumber, 1, (key, oldValue) => oldValue + 1);
                _logger?.LogDebug($"AttendedWeighingService: 缓存车牌识别结果: {plateNumber} (次数: {_plateNumberCache[plateNumber]})");
            }
        }
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

            // 如果状态发生变化，记录日志
            if (_currentStatus != previousStatus)
            {
                _logger?.LogInformation($"AttendedWeighingService: 状态变化 {previousStatus} -> {_currentStatus}, 当前重量: {weight}kg");
            }
        }
    }

    /// <summary>
    /// 处理重量变化
    /// </summary>
    private void ProcessWeightChange(decimal currentWeight)
    {
        _lastWeight = currentWeight;

        switch (_currentStatus)
        {
            case AttendedWeighingStatus.下称:
                // 下称 -> 上称等待重量稳定：重量从 <0.5t 增长到 >0.5t
                if (currentWeight > WeightThreshold)
                {
                    _currentStatus = AttendedWeighingStatus.上称等待重量稳定;
                    _stabilityStartTime = DateTime.UtcNow;
                    _stabilityBaseWeight = currentWeight;
                    _stableWeight = null;
                    
                    // 选择车牌（从缓存中取识别次数最多的）
                    SelectPlateNumberFromCache();
                    
                    _logger?.LogInformation($"AttendedWeighingService: 进入上称等待重量稳定状态，重量: {currentWeight}kg");
                }
                break;

            case AttendedWeighingStatus.上称等待重量稳定:
                if (currentWeight < WeightThreshold)
                {
                    // 称重未稳定流程：从上称等待重量稳定直接进入下称
                    _currentStatus = AttendedWeighingStatus.下称;
                    _stabilityStartTime = null;
                    _stabilityBaseWeight = null;
                    _stableWeight = null;
                    
                    // 对所有camera抓拍并打印日志（不需要保存照片）
                    _ = Task.Run(async () =>
                    {
                        var photos = await CaptureAllCamerasAsync("称重未稳定流程");
                        if (photos.Count == 0)
                        {
                            _logger?.LogWarning($"AttendedWeighingService: 称重未稳定流程抓拍完成，但没有获取到任何照片");
                        }
                        else
                        {
                            _logger?.LogInformation($"AttendedWeighingService: 称重未稳定流程抓拍了 {photos.Count} 张照片");
                        }
                    });
                    
                    // 清空车牌缓存
                    ClearPlateNumberCache();
                    
                    _logger?.LogWarning($"AttendedWeighingService: 称重未稳定流程，重量回到 {currentWeight}kg，已触发抓拍");
                }
                else
                {
                    // 检查是否稳定
                    CheckWeightStability(currentWeight);
                }
                break;

            case AttendedWeighingStatus.上称重量已稳定:
                if (currentWeight < WeightThreshold)
                {
                    // 上称重量已稳定 -> 下称：正常流程
                    _currentStatus = AttendedWeighingStatus.下称;
                    _stabilityStartTime = null;
                    _stabilityBaseWeight = null;
                    
                    // 再次判断是否还有其他更多次数的车牌，有则更新
                    UpdatePlateNumberIfNeeded();
                    
                    // 清空车牌缓存
                    ClearPlateNumberCache();
                    
                    _logger?.LogInformation($"AttendedWeighingService: 正常流程完成，进入下称状态，重量: {currentWeight}kg");
                }
                break;
        }
    }

    /// <summary>
    /// 检查重量稳定性
    /// </summary>
    private void CheckWeightStability(decimal currentWeight)
    {
        if (_stabilityBaseWeight == null)
        {
            _stabilityBaseWeight = currentWeight;
            _stabilityStartTime = DateTime.UtcNow;
            return;
        }

        // 检查重量是否在稳定范围内（±0.1t）
        var weightDifference = Math.Abs(currentWeight - _stabilityBaseWeight.Value);
        if (weightDifference > WeightStabilityTolerance)
        {
            // 波动超过范围，重新计时
            _stabilityBaseWeight = currentWeight;
            _stabilityStartTime = DateTime.UtcNow;
            _stableWeight = null;
            return;
        }

        // 检查是否稳定了3秒
        if (_stabilityStartTime.HasValue)
        {
            var elapsed = (DateTime.UtcNow - _stabilityStartTime.Value).TotalMilliseconds;
            if (elapsed >= StabilityDurationMs)
            {
                // 进入稳定状态
                if (_stableWeight == null)
                {
                    // 记录进入稳定状态瞬时的重量值
                    _stableWeight = currentWeight;
                    
                    // 选择车牌（从缓存中取识别次数最多的）
                    SelectPlateNumberFromCache();
                    
                    // 状态转换：上称等待重量稳定 -> 上称重量已稳定
                    _currentStatus = AttendedWeighingStatus.上称重量已稳定;
                    
                    _logger?.LogInformation($"AttendedWeighingService: 重量已稳定，稳定重量: {_stableWeight}kg");
                    
                    // 在上称重量已稳定时，进行抓拍并创建WeighingRecord
                    _ = Task.Run(async () => await OnWeightStabilizedAsync());
                }
            }
        }
    }

    /// <summary>
    /// 重量已稳定时的处理
    /// </summary>
    private async Task OnWeightStabilizedAsync()
    {
        try
        {
            // 抓拍所有相机
            var photoPaths = await CaptureAllCamerasAsync("上称重量已稳定");

            // 创建WeighingRecord（传入照片路径）
            await CreateWeighingRecordAsync(photoPaths);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "AttendedWeighingService: 处理重量稳定时发生错误");
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
                _logger?.LogWarning($"AttendedWeighingService: 没有配置相机，无法进行抓拍 ({reason})");
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
                    _logger?.LogWarning($"AttendedWeighingService: 相机配置无效: {cameraConfig.Name}");
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
                _logger?.LogWarning($"AttendedWeighingService: 没有有效的相机配置，无法进行抓拍 ({reason})");
                return new List<string>();
            }

            _logger?.LogInformation($"AttendedWeighingService: 开始抓拍 {requests.Count} 个相机 ({reason})");

            var results = await _hikvisionService.CaptureJpegFromStreamBatchAsync(requests);

            var successCount = results.Count(r => r.Success);
            var failCount = results.Count - successCount;

            _logger?.LogInformation($"AttendedWeighingService: 抓拍完成，成功: {successCount}, 失败: {failCount} ({reason})");

            // 记录失败的详细信息
            foreach (var result in results.Where(r => !r.Success))
            {
                _logger?.LogWarning($"AttendedWeighingService: 抓拍失败 - 设备: {result.Request.DeviceKey}, 通道: {result.Request.Channel}, 错误: {result.ErrorMessage}");
            }

            // 返回成功抓拍的照片路径列表
            var photoPaths = results.Where(r => r.Success && File.Exists(r.Request.SaveFullPath))
                .Select(r => r.Request.SaveFullPath)
                .ToList();

            // 如果照片列表为空，打印日志
            if (photoPaths.Count == 0)
            {
                _logger?.LogWarning($"AttendedWeighingService: 抓拍完成，但没有成功获取到任何照片 ({reason})");
            }

            return photoPaths;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"AttendedWeighingService: 抓拍所有相机时发生错误 ({reason})");
            _logger?.LogWarning($"AttendedWeighingService: 抓拍异常，返回空照片列表 ({reason})");
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
                // 使用进入稳定状态瞬时记录的重量值
                if (_stableWeight == null)
                {
                    _logger?.LogError("AttendedWeighingService: 无法创建WeighingRecord，稳定重量值为空");
                    return;
                }

                weight = _stableWeight.Value;
                plateNumber = _selectedPlateNumber;
            }

            using var uow = _unitOfWorkManager.Begin();

            // 创建称重记录
            var weighingRecord = new WeighingRecord(weight)
            {
                PlateNumber = plateNumber
            };

            await _weighingRecordRepository.InsertAsync(weighingRecord);
            await uow.CompleteAsync();

            _logger?.LogInformation($"AttendedWeighingService: 创建称重记录成功，ID: {weighingRecord.Id}, 重量: {weight}kg, 车牌: {plateNumber ?? "无"}");

            // 保存抓拍的照片到WeighingRecordAttachment
            if (photoPaths != null && photoPaths.Count > 0)
            {
                await SaveCapturePhotosAsync(weighingRecord.Id, photoPaths);
            }
            else
            {
                _logger?.LogWarning($"AttendedWeighingService: 称重记录 {weighingRecord.Id} 没有关联任何照片");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "AttendedWeighingService: 创建称重记录时发生错误");
        }
    }

    /// <summary>
    /// 保存抓拍的照片
    /// </summary>
    private async Task SaveCapturePhotosAsync(long weighingRecordId, List<string> photoPaths)
    {
        try
        {
            if (photoPaths == null || photoPaths.Count == 0)
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
                        _logger?.LogWarning($"AttendedWeighingService: 照片文件不存在: {photoPath}");
                        continue;
                    }

                    var fileName = Path.GetFileName(photoPath);
                    var attachmentFile = new AttachmentFile(fileName, photoPath, AttachType.EntryPhoto);

                    await _attachmentFileRepository.InsertAsync(attachmentFile);

                    var weighingRecordAttachment = new WeighingRecordAttachment(weighingRecordId, attachmentFile.Id);
                    await _weighingRecordAttachmentRepository.InsertAsync(weighingRecordAttachment);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, $"AttendedWeighingService: 保存照片失败: {photoPath}");
                }
            }

            await uow.CompleteAsync();
            _logger?.LogInformation($"AttendedWeighingService: 保存了 {photoPaths.Count} 张照片到称重记录 {weighingRecordId}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"AttendedWeighingService: 保存抓拍照片时发生错误");
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
            .OrderByDescending(kvp => kvp.Value)
            .FirstOrDefault();

        _selectedPlateNumber = mostFrequent.Key;
        _logger?.LogInformation($"AttendedWeighingService: 选择车牌: {_selectedPlateNumber} (识别次数: {mostFrequent.Value})");
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
            .OrderByDescending(kvp => kvp.Value)
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(mostFrequent.Key) &&
            (string.IsNullOrEmpty(_selectedPlateNumber) || mostFrequent.Value > _plateNumberCache.GetValueOrDefault(_selectedPlateNumber, 0)))
        {
            _selectedPlateNumber = mostFrequent.Key;
            _logger?.LogInformation($"AttendedWeighingService: 更新车牌: {_selectedPlateNumber} (识别次数: {mostFrequent.Value})");
        }
    }

    /// <summary>
    /// 清空车牌缓存
    /// </summary>
    private void ClearPlateNumberCache()
    {
        _plateNumberCache.Clear();
        _selectedPlateNumber = null;
        _logger?.LogDebug("AttendedWeighingService: 已清空车牌缓存");
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Stop();
    }
}
