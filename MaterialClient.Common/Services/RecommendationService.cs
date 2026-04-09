using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

/// <summary>
///     推荐服务接口，提供基于车牌号的推荐数据查询与缓存读取
/// </summary>
public interface IRecommendationService
{
    /// <summary>
    ///     根据车牌号从数据库查询最新已完成运单的推荐数据
    /// </summary>
    /// <param name="plateNumber">车牌号</param>
    /// <returns>推荐数据，如果未找到则返回 null</returns>
    Task<WaybillRecommendationDto?> GetRecommendationByPlateNumberAsync(string plateNumber);

    /// <summary>
    ///     根据车牌号从内存缓存读取最新推荐数据
    /// </summary>
    /// <param name="plateNumber">车牌号</param>
    /// <returns>缓存的推荐数据，如果未找到则返回 null</returns>
    Task<WaybillRecommendationDto?> GetLatestRecommendationAsync(string plateNumber);

    /// <summary>
    ///     更新推荐缓存（从运单实体构建缓存数据并写入）
    /// </summary>
    /// <param name="waybill">运单实体</param>
    void UpdateRecommendationCache(Waybill waybill);
}

/// <summary>
///     推荐服务实现，提供数据库查询和内存缓存双路径
/// </summary>
public class RecommendationService : IRecommendationService, ISingletonDependency
{
    private const string CacheKeyPrefix = "Recommendation_";
    private const string CacheIndexKey = "Recommendation_Index";
    private const int MaxCacheSize = 200;
    private const int EvictCount = 10;

    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly ILogger<RecommendationService> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public RecommendationService(
        IRepository<Waybill, long> waybillRepository,
        ILogger<RecommendationService> logger,
        IMemoryCache memoryCache,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _waybillRepository = waybillRepository;
        _logger = logger;
        _memoryCache = memoryCache;
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<WaybillRecommendationDto?> GetRecommendationByPlateNumberAsync(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
        {
            _logger.LogWarning("GetRecommendationByPlateNumberAsync: Plate number is null or empty");
            return null;
        }

        try
        {
            var waybillQuery = await _waybillRepository.GetQueryableAsync();
            var latestWaybill = await waybillQuery
                .AsNoTracking()
                .Where(w => w.OrderType == OrderTypeEnum.Completed)
                .Where(w => w.PlateNumber == plateNumber)
                .OrderByDescending(w => w.JoinTime ?? w.AddDate)
                .FirstOrDefaultAsync();

            if (latestWaybill == null)
            {
                _logger.LogInformation(
                    "GetRecommendationByPlateNumberAsync: No completed waybill found for plate number '{PlateNumber}'",
                    plateNumber);
                return null;
            }

            _logger.LogInformation(
                "GetRecommendationByPlateNumberAsync: Found recommendation for plate number '{PlateNumber}', WaybillId: {WaybillId}",
                plateNumber, latestWaybill.Id);

            var validProviderId = latestWaybill.ProviderId;

            if (validProviderId == null)
            {
                validProviderId =
                    (await waybillQuery
                        .FirstOrDefaultAsync(x => x.PlateNumber == plateNumber && x.ProviderId != null))
                    ?.ProviderId;
            }

            return new WaybillRecommendationDto(
                latestWaybill.MaterialId,
                validProviderId,
                latestWaybill.MaterialUnitId,
                latestWaybill.OrderPlanOnPcs
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetRecommendationByPlateNumberAsync: Error occurred while getting recommendation for plate number '{PlateNumber}'",
                plateNumber);
            return null;
        }
    }

    /// <inheritdoc />
    public Task<WaybillRecommendationDto?> GetLatestRecommendationAsync(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
            return Task.FromResult<WaybillRecommendationDto?>(null);

        try
        {
            using (_lock.ReadLock())
            {
                var cacheKey = BuildCacheKey(plateNumber);
                if (_memoryCache.TryGetValue(cacheKey, out WaybillRecommendationDto? dto))
                {
                    _logger.LogDebug(
                        "GetLatestRecommendationAsync: Cache hit for plate number '{PlateNumber}'",
                        plateNumber);
                    return Task.FromResult<WaybillRecommendationDto?>(dto);
                }
            }

            _logger.LogDebug(
                "GetLatestRecommendationAsync: Cache miss for plate number '{PlateNumber}'",
                plateNumber);
            return Task.FromResult<WaybillRecommendationDto?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetLatestRecommendationAsync: Error reading cache for plate number '{PlateNumber}'",
                plateNumber);
            return Task.FromResult<WaybillRecommendationDto?>(null);
        }
    }

    /// <inheritdoc />
    public void UpdateRecommendationCache(Waybill waybill)
    {
        if (waybill == null || string.IsNullOrWhiteSpace(waybill.PlateNumber))
            return;

        try
        {
            var dto = new WaybillRecommendationDto(
                waybill.MaterialId,
                waybill.ProviderId,
                waybill.MaterialUnitId,
                waybill.OrderPlanOnPcs
            );

            using (_lock.WriteLock())
            {
                var index = GetOrCreateIndex();
                var cacheKey = BuildCacheKey(waybill.PlateNumber);

                // 如果已存在，原地更新（移到最新位置）
                if (index.Remove(waybill.PlateNumber))
                {
                    _memoryCache.Set(cacheKey, dto, new MemoryCacheEntryOptions
                    {
                        Priority = CacheItemPriority.NeverRemove
                    });
                    index.Add(waybill.PlateNumber);
                    UpdateIndex(index);
                    _logger.LogDebug(
                        "UpdateRecommendationCache: Updated existing cache for plate number '{PlateNumber}'",
                        waybill.PlateNumber);
                    return;
                }

                // 缓存已达上限，执行 LRU 淘汰
                if (index.Count >= MaxCacheSize)
                {
                    var evictKeys = index.Take(EvictCount).ToList();
                    foreach (var evictKey in evictKeys)
                    {
                        _memoryCache.Remove(BuildCacheKey(evictKey));
                        index.Remove(evictKey);
                    }

                    _logger.LogDebug(
                        "UpdateRecommendationCache: Evicted {Count} oldest entries", evictKeys.Count);
                }

                // 写入新缓存
                _memoryCache.Set(cacheKey, dto, new MemoryCacheEntryOptions
                {
                    Priority = CacheItemPriority.NeverRemove
                });
                index.Add(waybill.PlateNumber);
                UpdateIndex(index);

                _logger.LogInformation(
                    "UpdateRecommendationCache: Cached recommendation for plate number '{PlateNumber}', cache size: {Count}",
                    waybill.PlateNumber, index.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "UpdateRecommendationCache: Error updating cache for plate number '{PlateNumber}'",
                waybill.PlateNumber);
        }
    }

    private static string BuildCacheKey(string plateNumber) => $"{CacheKeyPrefix}{plateNumber}";

    private List<string> GetOrCreateIndex()
    {
        return _memoryCache.GetOrCreate(CacheIndexKey, entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            return new List<string>();
        })!;
    }

    private void UpdateIndex(List<string> index)
    {
        _memoryCache.Set(CacheIndexKey, index, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove
        });
    }
}
