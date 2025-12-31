using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Entities;
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
}

/// <summary>
///     材料服务实现
/// </summary>
public class MaterialService : DomainService, IMaterialService
{
    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<MaterialUnit, int> _materialUnitRepository;
    private readonly IRepository<Provider, int> _providerRepository;

    public MaterialService(
        IRepository<Material, int> materialRepository,
        IRepository<MaterialUnit, int> materialUnitRepository,
        IRepository<Provider, int> providerRepository)
    {
        _materialRepository = materialRepository;
        _materialUnitRepository = materialUnitRepository;
        _providerRepository = providerRepository;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<PagedResultDto<Material>> GetPagedMaterialsAsync(
        string? searchText = null,
        int pageIndex = 1,
        int pageSize = 10)
    {
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
        var queryable = await _materialRepository.GetQueryableAsync();
        queryable = queryable.AsNoTracking();
        
        // 只查询未删除的记录
        queryable = queryable.Where(m => !m.IsDeleted);

        var materials = await queryable
            .OrderBy(m => m.Name)
            .ToListAsync();

        return materials;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<List<MaterialUnit>> GetMaterialUnitsByMaterialIdAsync(int materialId)
    {
        var queryable = await _materialUnitRepository.GetQueryableAsync();
        queryable = queryable.AsNoTracking();

        // 只查询未删除的记录
        queryable = queryable.Where(u => !u.IsDeleted && u.MaterialId == materialId);

        var units = await queryable
            .OrderBy(u => u.UnitName)
            .ToListAsync();

        return units;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<List<Provider>> GetAllProvidersAsync()
    {
        var queryable = await _providerRepository.GetQueryableAsync();
        queryable = queryable.AsNoTracking();

        // 只查询未删除的记录
        queryable = queryable.Where(p => !p.IsDeleted);

        var providers = await queryable
            .OrderBy(p => p.ProviderName)
            .ToListAsync();

        return providers;
    }
}

