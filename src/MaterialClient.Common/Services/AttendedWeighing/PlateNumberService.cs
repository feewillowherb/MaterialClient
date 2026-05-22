using System.Collections.Concurrent;
using MaterialClient.Common.Events;
using MaterialClient.Common.Providers;
using MaterialClient.Common.Services.AttendedWeighing.Records;
using MaterialClient.Common.Services.Vzvision;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.Common.Services.AttendedWeighing;

/// <summary>
///     车牌号管理服务接口
/// </summary>
public interface IPlateNumberService
{
    /// <summary>
    ///     处理识别到的车牌号
    /// </summary>
    void OnPlateNumberRecognized(string plateNumber, VzvisionColorType? colorType = null);

    /// <summary>
    ///     获取最频繁识别的车牌号（含优先级逻辑）
    /// </summary>
    string? GetMostFrequentPlateNumber();

    /// <summary>
    ///     清空车牌缓存
    /// </summary>
    void ClearCache();

    /// <summary>
    ///     移除指定车牌（用于幽灵道闸会话重置）
    /// </summary>
    void RemovePlate(string plateNumber);

    /// <summary>
    ///     更新运行时配置
    /// </summary>
    void UpdateConfiguration(bool enableLatestPlateNumber, bool enablePlateRewrite);

    /// <summary>
    ///     初始化车牌颜色优先级配置
    /// </summary>
    void InitializeColorFilter(HashSet<VzvisionColorType> lowPriorityColors);
}

/// <summary>
///     车牌号管理服务
///     管理车牌识别缓存、优先级选择、颜色过滤和推荐集成
/// </summary>
public class PlateNumberService : IPlateNumberService, ISingletonDependency
{
    /// <summary>
    ///     高优先级车牌的有效时间窗口（仅在此时间内的缓存记录参与高优先级筛选）
    /// </summary>
    private static readonly TimeSpan HighPriorityPlateWindow = TimeSpan.FromMinutes(20);

    private readonly ConcurrentDictionary<string, PlateNumberCacheRecord> _plateNumberCache = new();
    private readonly RecommendPlateNumberService _recommendPlateNumberService;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<PlateNumberService> _logger;

    // Configuration fields
    private bool _enableLatestPlateNumber;
    private bool _enablePlateRewrite;

    // Plate color priority config
    private bool _plateColorFilterInitialized;
    private HashSet<VzvisionColorType> _lowPriorityPlateColors = new();

    public PlateNumberService(
        RecommendPlateNumberService recommendPlateNumberService,
        ILocalEventBus localEventBus,
        ILogger<PlateNumberService> logger)
    {
        _recommendPlateNumberService = recommendPlateNumberService;
        _localEventBus = localEventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public void OnPlateNumberRecognized(string plateNumber, VzvisionColorType? colorType = null)
    {
        if (string.IsNullOrWhiteSpace(plateNumber)) return;

        // Log low-priority plate colors (but don't reject them)
        if (colorType.HasValue && _lowPriorityPlateColors.Contains(colorType.Value))
        {
            _logger.LogInformation("检测到低优先级车牌颜色: Plate={Plate}, Color={Color}",
                plateNumber, colorType.Value);
        }

        // Filter out "挂" character
        var filteredPlateNumber = PlateNumberValidator.FilterHangingCharacter(plateNumber, _logger);
        if (string.IsNullOrWhiteSpace(filteredPlateNumber)) return;

        // Get recommended plate number
        var recommendedPlateNumber = _recommendPlateNumberService.GetRecommendPlateNumber(filteredPlateNumber);

        if (recommendedPlateNumber != filteredPlateNumber)
        {
            _logger.LogInformation(
                "车牌号推荐匹配: 原始={OriginalPlate}, 推荐={RecommendedPlate}",
                filteredPlateNumber,
                recommendedPlateNumber);
        }

        var finalPlateNumber = recommendedPlateNumber;

        // Update plate cache
        _plateNumberCache.AddOrUpdate(
            finalPlateNumber,
            new PlateNumberCacheRecord
            {
                Count = 1,
                LastUpdateTime = DateTime.UtcNow,
                ColorType = colorType,
                LockedAt = _enablePlateRewrite ? null : DateTime.UtcNow
            },
            (key, oldValue) => new PlateNumberCacheRecord
            {
                Count = oldValue.Count + 1,
                LastUpdateTime = DateTime.UtcNow,
                ColorType = colorType ?? oldValue.ColorType,
                LockedAt = !_enablePlateRewrite
                    ? (oldValue.LockedAt ?? DateTime.UtcNow)
                    : oldValue.LockedAt
            });

        // Get most frequent plate and publish notification
        var mostFrequent = GetMostFrequentPlateNumber();
        _ = _localEventBus.PublishAsync(new PlateNumberChangedEventData(mostFrequent));
    }

    /// <inheritdoc />
    public string? GetMostFrequentPlateNumber()
    {
        if (_plateNumberCache.IsEmpty) return null;

        // LockedAt priority: when plate rewrite is off
        var lockedCandidates = _plateNumberCache
            .Where(kvp => kvp.Value.LockedAt.HasValue)
            .OrderBy(kvp => kvp.Value.LockedAt!.Value)
            .ToList();

        if (lockedCandidates.Count > 0)
        {
            _logger.LogWarning(
                "车牌重写已关闭，使用 LockedAt 优先选择车牌: Plate={Plate}, LockedAt={LockedAt}, Color={Color}",
                lockedCandidates[0].Key, lockedCandidates[0].Value.LockedAt, lockedCandidates[0].Value.ColorType);

            if (!string.IsNullOrWhiteSpace(lockedCandidates[0].Key))
            {
                return lockedCandidates[0].Key;
            }
        }

        // Separate high-priority and low-priority plates
        var highPriorityCutoff = DateTime.UtcNow - HighPriorityPlateWindow;
        var highPriorityPlates = _plateNumberCache
            .Where(kvp => kvp.Value.LastUpdateTime >= highPriorityCutoff)
            .Where(kvp => !kvp.Value.ColorType.HasValue || !_lowPriorityPlateColors.Contains(kvp.Value.ColorType.Value))
            .ToList();

        if (highPriorityPlates.Count > 0)
        {
            var mostFrequent = highPriorityPlates
                .OrderByDescending(kvp =>
                    _enableLatestPlateNumber ? kvp.Value.LastUpdateTime.Ticks : kvp.Value.Count)
                .ThenByDescending(kvp => kvp.Value.Count)
                .First();
            return mostFrequent.Key;
        }

        // Fall back to low-priority plates
        var lowPriorityMostFrequent = _plateNumberCache
            .OrderByDescending(kvp =>
                _enableLatestPlateNumber ? kvp.Value.LastUpdateTime.Ticks : kvp.Value.Count)
            .ThenByDescending(kvp => kvp.Value.Count)
            .First();

        _logger.LogInformation("使用低优先级车牌（无高优先级车牌可用）: Plate={Plate}, Color={Color}",
            lowPriorityMostFrequent.Key, lowPriorityMostFrequent.Value.ColorType);

        return lowPriorityMostFrequent.Key;
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        _plateNumberCache.Clear();
        _logger.LogDebug("Cleared plate number cache");

        _ = _localEventBus.PublishAsync(new PlateNumberChangedEventData(null));
    }

    /// <inheritdoc />
    public void RemovePlate(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
            return;

        var keysToRemove = _plateNumberCache.Keys
            .Where(k => string.Equals(k, plateNumber, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _plateNumberCache.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogDebug(
                "幽灵会话重置: 已从车牌缓存移除废弃键 AbandonedPlate={AbandonedPlate}, RemovedCount={Count}",
                plateNumber, keysToRemove.Count);
        }
    }

    /// <inheritdoc />
    public void UpdateConfiguration(bool enableLatestPlateNumber, bool enablePlateRewrite)
    {
        _enableLatestPlateNumber = enableLatestPlateNumber;
        _enablePlateRewrite = enablePlateRewrite;
    }

    /// <inheritdoc />
    public void InitializeColorFilter(HashSet<VzvisionColorType> lowPriorityColors)
    {
        if (_plateColorFilterInitialized) return;

        _lowPriorityPlateColors = lowPriorityColors ?? new HashSet<VzvisionColorType>();
        _plateColorFilterInitialized = true;

        _logger.LogInformation("Loaded low-priority plate colors: {Colors}",
            _lowPriorityPlateColors.Count == 0 ? "none" : string.Join(", ", _lowPriorityPlateColors));
    }
}
