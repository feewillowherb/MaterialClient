using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

/// <summary>
///     材料服务接口
/// </summary>
public interface IMaterialService
{
    /// <summary>
    ///     分页查询材料列表
    /// </summary>
    /// <param name="searchText">搜索关键字（可选）</param>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>分页结果，包含总数和当前页数据</returns>
    Task<PagedResultDto<Material>> GetPagedMaterialsAsync(
        string? searchText = null,
        int pageIndex = 1,
        int pageSize = 10);

    /// <summary>
    ///     获取所有材料列表（未删除的）
    /// </summary>
    /// <returns>材料列表，按名称排序</returns>
    Task<List<Material>> GetAllMaterialsAsync();

    /// <summary>
    ///     根据材料ID获取材料单位列表
    /// </summary>
    /// <param name="materialId">材料ID</param>
    /// <returns>材料单位列表，按单位名称排序</returns>
    Task<List<MaterialUnit>> GetMaterialUnitsByMaterialIdAsync(int materialId);

    /// <summary>
    ///     获取所有供应商列表（未删除的）
    /// </summary>
    /// <returns>供应商列表，按供应商名称排序</returns>
    Task<List<Provider>> GetAllProvidersAsync();

    /// <summary>
    ///     分页查询供应商列表
    /// </summary>
    /// <param name="searchText">搜索关键字（可选）</param>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>分页结果，包含总数和当前页数据</returns>
    Task<PagedResultDto<ProviderDto>> GetPagedProvidersAsync(
        string? searchText = null,
        int pageIndex = 1,
        int pageSize = 10);

    /// <summary>
    ///     新增供应商
    /// </summary>
    /// <param name="providerName">供应商名称</param>
    /// <param name="deliveryType">当前称重记录/联单的 DeliveryType</param>
    Task<Provider> CreateProviderAsync(string providerName, DeliveryType deliveryType);

    /// <summary>
    ///     新增材料（默认单位: 个，换算率: 1:1）
    /// </summary>
    /// <param name="materialName">材料名称</param>
    Task<Material> CreateMaterialAsync(string materialName);
}

/// <summary>
///     材料服务实现
/// </summary>
public class MaterialService : DomainService, IMaterialService
{
    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<MaterialUnit, int> _materialUnitRepository;
    private readonly IRepository<Provider, int> _providerRepository;
    private readonly ISettingsService _settingsService;

    public MaterialService(
        IRepository<Material, int> materialRepository,
        IRepository<MaterialUnit, int> materialUnitRepository,
        IRepository<Provider, int> providerRepository,
        ISettingsService settingsService)
    {
        _materialRepository = materialRepository;
        _materialUnitRepository = materialUnitRepository;
        _providerRepository = providerRepository;
        _settingsService = settingsService;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<PagedResultDto<Material>> GetPagedMaterialsAsync(
        string? searchText = null,
        int pageIndex = 1,
        int pageSize = 10)
    {
        var weighingMode = await _settingsService.GetWeighingModeAsync();

        // 构建查询条件
        var queryable = await _materialRepository.GetQueryableAsync();

        queryable = queryable.AsNoTracking();

        // 应用搜索过滤
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.Trim();
            queryable = queryable.Where(m =>
                (m.Name != null && m.Name.Contains(search)) //||
                // (m.Specifications != null && m.Specifications.Contains(search)) ||
                // (m.Size != null && m.Size.Contains(search)) ||
                // (m.Code != null && m.Code.Contains(search))
            );
        }

        // 只查询未删除的记录
        queryable = queryable.Where(m => !m.IsDeleted);

        // 按系统称重模式过滤
        queryable = queryable.Where(m => m.WeighingMode == weighingMode);

        // 获取总数
        var totalCount = await queryable.CountAsync();

        // 分页查询
        var skipCount = (pageIndex - 1) * pageSize;
        var items = await queryable
            .OrderBy(m => m.Name)
            .Skip(skipCount)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<Material>(totalCount, items);
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<List<Material>> GetAllMaterialsAsync()
    {
        var weighingMode = await _settingsService.GetWeighingModeAsync();

        var queryable = await _materialRepository.GetQueryableAsync();
        queryable = queryable.AsNoTracking();
        
        // 只查询未删除的记录
        queryable = queryable.Where(m => !m.IsDeleted);

        // 按系统称重模式过滤
        queryable = queryable.Where(m => m.WeighingMode == weighingMode);

        var materials = await queryable
            .OrderBy(m => m.Name)
            .ToListAsync();

        return materials;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<List<MaterialUnit>> GetMaterialUnitsByMaterialIdAsync(int materialId)
    {
        var weighingMode = await _settingsService.GetWeighingModeAsync();

        var queryable = await _materialUnitRepository.GetQueryableAsync();
        queryable = queryable.AsNoTracking();

        // 只查询未删除的记录
        queryable = queryable.Where(u => !u.IsDeleted && u.MaterialId == materialId && u.WeighingMode == weighingMode);

        var units = await queryable
            .OrderBy(u => u.UnitName)
            .ToListAsync();

        return units;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<List<Provider>> GetAllProvidersAsync()
    {
        var weighingMode = await _settingsService.GetWeighingModeAsync();

        var queryable = await _providerRepository.GetQueryableAsync();
        queryable = queryable.AsNoTracking();

        // 只查询未删除的记录
        queryable = queryable.Where(p => !p.IsDeleted);

        // 按系统称重模式过滤
        queryable = queryable.Where(p => p.WeighingMode == weighingMode);

        var providers = await queryable
            .OrderBy(p => p.ProviderName)
            .ToListAsync();

        return providers;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<PagedResultDto<ProviderDto>> GetPagedProvidersAsync(
        string? searchText = null,
        int pageIndex = 1,
        int pageSize = 10)
    {
        var weighingMode = await _settingsService.GetWeighingModeAsync();

        var queryable = await _providerRepository.GetQueryableAsync();
        queryable = queryable.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.Trim();
            queryable = queryable.Where(p => p.ProviderName != null && p.ProviderName.Contains(search));
        }

        queryable = queryable.Where(p => !p.IsDeleted);
        queryable = queryable.Where(p => p.WeighingMode == weighingMode);

        var totalCount = await queryable.CountAsync();

        var skipCount = (pageIndex - 1) * pageSize;
        var items = await queryable
            .OrderBy(p => p.ProviderName)
            .Skip(skipCount)
            .Take(pageSize)
            .Select(p => new ProviderDto
            {
                Id = p.Id,
                ProviderType = p.ProviderType ?? 0,
                ProviderName = p.ProviderName ?? string.Empty,
                ContactName = p.ContectName,
                ContactPhone = p.ContectPhone
            })
            .ToListAsync();

        return new PagedResultDto<ProviderDto>(totalCount, items);
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<Provider> CreateProviderAsync(string providerName, DeliveryType deliveryType)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name is required.", nameof(providerName));
        }

        var weighingMode = await _settingsService.GetWeighingModeAsync();
        var now = DateTime.Now;

        var provider = new Provider(
            providerType: (int)deliveryType,
            providerName: providerName.Trim())
        {
            CoId = 1, // TODO update in next version
            WeighingMode = weighingMode,
            AddDate = now,
            AddTime = (int)DateTimeOffset.Now.ToUnixTimeSeconds(),
            IsDeleted = false
        };

        return await _providerRepository.InsertAsync(provider, autoSave: true);
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<Material> CreateMaterialAsync(string materialName)
    {
        if (string.IsNullOrWhiteSpace(materialName))
        {
            throw new ArgumentException("Material name is required.", nameof(materialName));
        }

        var weighingMode = await _settingsService.GetWeighingModeAsync();
        var now = DateTime.Now;

        var material = new Material(
            name: materialName.Trim(),
            coId: 1) // TODO update in next version
        {
            UnitName = "个",
            UnitRate = 1,
            WeighingMode = weighingMode,
            AddDate = now,
            AddTime = (int)DateTimeOffset.Now.ToUnixTimeSeconds(),
            IsDeleted = false
        };

        material = await _materialRepository.InsertAsync(material, autoSave: true);

        // Create default MaterialUnit to keep downstream unit loading consistent.
        var defaultUnit = new MaterialUnit(
            materialId: material.Id,
            unitName: "个",
            rate: 1m)
        {
            UnitCalculationType = 1, // 按数量
            WeighingMode = weighingMode,
            AddDate = now,
            AddTime = (int)DateTimeOffset.Now.ToUnixTimeSeconds(),
            IsDeleted = false
        };

        await _materialUnitRepository.InsertAsync(defaultUnit, autoSave: true);

        return material;
    }
}

