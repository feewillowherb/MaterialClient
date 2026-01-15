using MaterialClient.Common.Entities;
using MaterialClient.Common.Extensions;
using MaterialClient.Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Providers;

/// <summary>
///     车牌号推荐服务
///     使用IMemoryCache管理车牌号缓存，永久性缓存，满时自动清理最后10个，线程安全
/// </summary>
public class RecommendPlateNumberService : DomainService, ISingletonDependency
{
    private const string CacheKey = "PlateNumbers";
    private const int MaxCacheSize = 200;

    private readonly ILogger<RecommendPlateNumberService> _logger;
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly ISettingsService _settingsService;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IMemoryCache _memoryCache;
    private readonly ReaderWriterLockSlim _lock = new();

    private int _minDiffCharCount = 1; // 缓存的配置值

    public RecommendPlateNumberService(
        IRepository<Waybill, long> waybillRepository,
        ILogger<RecommendPlateNumberService> logger,
        ISettingsService settingsService,
        IUnitOfWorkManager unitOfWorkManager,
        IMemoryCache memoryCache)
    {
        _waybillRepository = waybillRepository;
        _logger = logger;
        _settingsService = settingsService;
        _unitOfWorkManager = unitOfWorkManager;
        _memoryCache = memoryCache;
    }

    /// <summary>
    ///     初始化缓存，从数据库加载最新200条车牌号，并缓存配置
    /// </summary>
    public async Task InitializeCacheAsync()
    {
        using (var uow = _unitOfWorkManager.Begin())
        {
            try
            {
                // 加载配置
                var settings = await _settingsService.GetSettingsAsync();
                _minDiffCharCount = settings.SystemSettings.MinDiffCharCount;
                
                // 限制在 0-2 范围内
                if (_minDiffCharCount < 0) _minDiffCharCount = 0;
                if (_minDiffCharCount > 2) _minDiffCharCount = 2;

                // 从数据库加载车牌号
                var queryable = await _waybillRepository.GetQueryableAsync();
                var plateNumbers = await queryable
                    .Where(w => !string.IsNullOrWhiteSpace(w.PlateNumber))
                    .OrderByDescending(w => w.AddDate)
                    .Select(w => w.PlateNumber!)
                    .Distinct()
                    .Take(MaxCacheSize)
                    .ToListAsync();

                using (_lock.WriteLock())
                {
                    // 设置永久性缓存（无过期时间）
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        Priority = CacheItemPriority.NeverRemove, // 永久缓存
                        Size = 1 // 缓存项大小
                    };

                    _memoryCache.Set(CacheKey, plateNumbers, cacheOptions);
                }

                _logger.LogInformation(
                    "车牌号推荐服务缓存初始化完成，加载了 {Count} 条车牌号，配置差异数={MinDiffCharCount}",
                    plateNumbers.Count, _minDiffCharCount);
                await uow.CompleteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化车牌号推荐服务缓存失败");
                // 设置空缓存（永久性缓存）
                using (_lock.WriteLock())
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        Priority = CacheItemPriority.NeverRemove, // 永久缓存
                        Size = 1
                    };
                    _memoryCache.Set(CacheKey, new List<string>(), cacheOptions);
                }
            }
        }
    }

    /// <summary>
    ///     根据输入的车牌号，从缓存中推荐最匹配的车牌号
    /// </summary>
    /// <param name="plateNumber">输入的车牌号</param>
    /// <returns>推荐的车牌号，如果未找到匹配则返回原始输入</returns>
    public string GetRecommendPlateNumber(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
            return plateNumber;

        try
        {
            List<string>? cachedPlates;
            using (_lock.ReadLock())
            {
                cachedPlates = _memoryCache.Get<List<string>>(CacheKey);
            }

            // 如果缓存为空，返回原始输入
            if (cachedPlates == null || !cachedPlates.Any())
            {
                return plateNumber;
            }

            // 遍历缓存查找匹配
            string? bestMatch = null;
            int bestDiff = int.MaxValue;

            foreach (var cachedPlate in cachedPlates)
            {
                if (string.IsNullOrWhiteSpace(cachedPlate))
                    continue;

                var diff = CalculateCharDiff(plateNumber, cachedPlate);

                if (diff <= _minDiffCharCount && diff < bestDiff)
                {
                    bestMatch = cachedPlate;
                    bestDiff = diff;
                }
            }

            // 如果找到匹配，记录日志并返回
            if (bestMatch != null)
            {
                _logger.LogInformation(
                    "车牌号推荐匹配成功: 输入={InputPlate}, 推荐={RecommendedPlate}, 差异数={DiffCount}",
                    plateNumber,
                    bestMatch,
                    bestDiff);
                return bestMatch;
            }

            // 未找到匹配，返回原始输入
            return plateNumber;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取推荐车牌号时发生异常: {PlateNumber}", plateNumber);
            return plateNumber;
        }
    }

    /// <summary>
    ///     添加车牌号到缓存（当 Waybill 完成时调用）
    ///     使用LRU策略，如果缓存已满则移除最老的数据
    /// </summary>
    /// <param name="plateNumber">要添加的车牌号</param>
    public void AddPlateNumberToCache(string? plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
            return;

        try
        {
            using (_lock.WriteLock())
            {
                var cachedPlates = _memoryCache.Get<List<string>>(CacheKey) ?? new List<string>();

                // 检查是否已存在（避免重复）
                if (cachedPlates.Contains(plateNumber))
                {
                    _logger.LogDebug(
                        "车牌号已存在于缓存中，跳过添加: {PlateNumber}",
                        plateNumber);
                    return;
                }

                // 如果缓存已满，移除最后10个
                if (cachedPlates.Count >= MaxCacheSize)
                {
                    var removeCount = Math.Min(10, cachedPlates.Count);
                    cachedPlates.RemoveRange(cachedPlates.Count - removeCount, removeCount);
                    _logger.LogDebug("缓存已满，移除了最后 {Count} 个车牌号", removeCount);
                }

                // 添加新车牌号到末尾
                cachedPlates.Add(plateNumber);

                // 更新缓存（永久性缓存）
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    Priority = CacheItemPriority.NeverRemove, // 永久缓存
                    Size = 1
                };

                _memoryCache.Set(CacheKey, cachedPlates, cacheOptions);

                _logger.LogInformation(
                    "已将车牌号添加到推荐服务缓存: {PlateNumber}, 当前缓存大小: {Count}",
                    plateNumber,
                    cachedPlates.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加车牌号到推荐服务缓存时发生异常: {PlateNumber}", plateNumber);
        }
    }

    /// <summary>
    ///     计算两个字符串的字符差异数
    ///     比较两个字符串在相同位置上的不同字符数量（长度不同时，差异数 = 长度差 + 位置差异数）
    /// </summary>
    private int CalculateCharDiff(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return Math.Max(str1?.Length ?? 0, str2?.Length ?? 0);

        int maxLen = Math.Max(str1.Length, str2.Length);
        int diffCount = 0;

        for (int i = 0; i < maxLen; i++)
        {
            char c1 = i < str1.Length ? str1[i] : '\0';
            char c2 = i < str2.Length ? str2[i] : '\0';
            if (c1 != c2) diffCount++;
        }

        return diffCount;
    }
}
