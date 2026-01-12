using System.Collections.Concurrent;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Providers;

/// <summary>
///     车牌号推荐服务
///     从数据库中加载最新200条车牌号到内存缓存，并根据配置的最小字符差异数进行匹配推荐
/// </summary>
public class RecommandPlateNumberService : DomainService, ISingletonDependency
{
    private readonly ILogger<RecommandPlateNumberService> _logger;
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly ISettingsService _settingsService;

    private volatile ConcurrentQueue<string> _plateNumberCache = new();

    public RecommandPlateNumberService(
        IRepository<Waybill, long> waybillRepository,
        ILogger<RecommandPlateNumberService> logger,
        ISettingsService settingsService)
    {
        _waybillRepository = waybillRepository;
        _logger = logger;
        _settingsService = settingsService;
    }

    /// <summary>
    ///     初始化缓存，从数据库加载最新200条车牌号
    /// </summary>
    [UnitOfWork]
    public async Task InitializeCacheAsync()
    {
        try
        {
            var queryable = await _waybillRepository.GetQueryableAsync();
            var plateNumbers = await queryable
                .Where(w => !string.IsNullOrWhiteSpace(w.PlateNumber))
                .OrderByDescending(w => w.AddDate)
                .Select(w => w.PlateNumber!)
                .Distinct()
                .Take(200)
                .ToListAsync();

            var newCache = new ConcurrentQueue<string>();
            foreach (var plateNumber in plateNumbers)
            {
                newCache.Enqueue(plateNumber);
            }

            _plateNumberCache = newCache;

            _logger.LogInformation(
                "车牌号推荐服务缓存初始化完成，加载了 {Count} 条车牌号",
                plateNumbers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化车牌号推荐服务缓存失败");
            // 使用空缓存
            _plateNumberCache = new ConcurrentQueue<string>();
        }
    }

    /// <summary>
    ///     根据输入的车牌号，从缓存中推荐最匹配的车牌号
    /// </summary>
    /// <param name="plateNumber">输入的车牌号</param>
    /// <returns>推荐的车牌号，如果未找到匹配则返回原始输入</returns>
    public string GetRecommandPlateNumber(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
            return plateNumber;

        try
        {
            // 获取当前缓存的引用（volatile 读取）
            var cache = _plateNumberCache;

            // 获取配置的最小字符差异数
            var settings = _settingsService.GetSettingsAsync().GetAwaiter().GetResult();
            var minDiffCharCount = settings.SystemSettings.MinDiffCharCount;

            // 限制在 0-2 范围内
            if (minDiffCharCount < 0) minDiffCharCount = 0;
            if (minDiffCharCount > 2) minDiffCharCount = 2;

            // 遍历队列查找匹配
            string? bestMatch = null;
            int bestDiff = int.MaxValue;

            foreach (var cachedPlate in cache)
            {
                if (string.IsNullOrWhiteSpace(cachedPlate))
                    continue;

                var diff = CalculateCharDiff(plateNumber, cachedPlate);

                if (diff <= minDiffCharCount && diff < bestDiff)
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
    /// </summary>
    /// <param name="plateNumber">要添加的车牌号</param>
    public void AddPlateNumberToCache(string? plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
            return;

        try
        {
            var cache = _plateNumberCache;

            // 检查缓存大小，如果已满200条则跳过
            if (cache.Count >= 200)
            {
                _logger.LogDebug(
                    "车牌号推荐服务缓存已满（200条），跳过添加车牌号: {PlateNumber}",
                    plateNumber);
                return;
            }

            // 检查是否已存在（避免重复）
            if (cache.Contains(plateNumber))
            {
                _logger.LogDebug(
                    "车牌号已存在于缓存中，跳过添加: {PlateNumber}",
                    plateNumber);
                return;
            }

            // 创建新缓存并添加新元素
            var newCache = new ConcurrentQueue<string>();
            foreach (var existingPlate in cache)
            {
                newCache.Enqueue(existingPlate);
            }

            newCache.Enqueue(plateNumber);

            // 原子替换引用
            _plateNumberCache = newCache;

            _logger.LogInformation(
                "已将车牌号添加到推荐服务缓存: {PlateNumber}, 当前缓存大小: {Count}",
                plateNumber,
                newCache.Count);
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
