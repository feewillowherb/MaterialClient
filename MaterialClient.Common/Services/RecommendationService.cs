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
///     推荐服务接口，提供推荐数据查询与全局缓存读取
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
    ///     从全局内存缓存读取最新推荐数据
    /// </summary>
    /// <returns>缓存的推荐数据，如果未找到则返回 null</returns>
    Task<WaybillRecommendationDto?> GetLatestRecommendationAsync();

    /// <summary>
    ///     更新推荐缓存（从运单实体构建缓存数据并写入）
    /// </summary>
    /// <param name="waybill">运单实体</param>
    void UpdateRecommendationCache(Waybill waybill);
}

/// <summary>
///     推荐服务实现，提供数据库查询和全局内存缓存双路径。
///     缓存使用全局唯一键存储单个推荐数据，任意运单完成时直接覆盖。
/// </summary>
public class RecommendationService : IRecommendationService, ISingletonDependency
{
    private const string GlobalCacheKey = "Recommendation_Global";

    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly ILogger<RecommendationService> _logger;
    private readonly IMemoryCache _memoryCache;
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
    public Task<WaybillRecommendationDto?> GetLatestRecommendationAsync()
    {
        try
        {
            if (_memoryCache.TryGetValue(GlobalCacheKey, out WaybillRecommendationDto? dto))
            {
                _logger.LogDebug("GetLatestRecommendationAsync: Cache hit");
                return Task.FromResult<WaybillRecommendationDto?>(dto);
            }

            _logger.LogDebug("GetLatestRecommendationAsync: Cache miss");
            return Task.FromResult<WaybillRecommendationDto?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLatestRecommendationAsync: Error reading cache");
            return Task.FromResult<WaybillRecommendationDto?>(null);
        }
    }

    /// <inheritdoc />
    public void UpdateRecommendationCache(Waybill waybill)
    {
        if (waybill == null)
            return;

        try
        {
            var dto = new WaybillRecommendationDto(
                waybill.MaterialId,
                waybill.ProviderId,
                waybill.MaterialUnitId,
                waybill.OrderPlanOnPcs
            );

            _memoryCache.Set(GlobalCacheKey, dto, new MemoryCacheEntryOptions
            {
                Priority = CacheItemPriority.NeverRemove
            });

            _logger.LogInformation(
                "UpdateRecommendationCache: Cached recommendation, WaybillId: {WaybillId}",
                waybill.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "UpdateRecommendationCache: Error updating cache for WaybillId: {WaybillId}",
                waybill.Id);
        }
    }
}
