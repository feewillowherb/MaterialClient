using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
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
    /// <param name="selectedIds">已选 id 列表，保证这些项出现在当前页结果前部</param>
    /// <returns>分页结果，Items 条数为 pageSize + selectedIds.Count（或更少）</returns>
    Task<PagedResultDto<Material>> GetPagedMaterialsAsync(
        string? searchText = null,
        int pageIndex = 1,
        int pageSize = 10,
        IReadOnlyList<int>? selectedIds = null);

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
    private readonly IRepository<UserSession, Guid> _userSessionRepository;
    private readonly IMaterialPlatformApi _materialPlatformApi;

    public MaterialService(
        IRepository<Material, int> materialRepository,
        IRepository<MaterialUnit, int> materialUnitRepository,
        IRepository<UserSession, Guid> userSessionRepository,
        IMaterialPlatformApi materialPlatformApi)
    {
        _materialRepository = materialRepository;
        _materialUnitRepository = materialUnitRepository;
        _userSessionRepository = userSessionRepository;
        _materialPlatformApi = materialPlatformApi;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<PagedResultDto<Material>> GetPagedMaterialsAsync(
        string? searchText = null,
        int pageIndex = 1,
        int pageSize = 10,
        IReadOnlyList<int>? selectedIds = null)
    {
        var queryable = await _materialRepository.GetQueryableAsync();
        queryable = queryable.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.Trim();
            queryable = queryable.Where(m =>
                (m.Name != null && m.Name.Contains(search)));
        }

        queryable = queryable.Where(m => !m.IsDeleted);

        var totalCount = await queryable.CountAsync();

        var merged = new List<Material>();

        if (selectedIds != null && selectedIds.Count > 0)
        {
            var selectedList = await queryable
                .Where(m => selectedIds.Contains(m.Id))
                .OrderBy(m => m.Name)
                .ToListAsync();
            var selectedSet = selectedList.Select(m => m.Id).ToHashSet();
            merged.AddRange(selectedList);

            var pageQuery = queryable
                .Where(m => !selectedSet.Contains(m.Id))
                .OrderBy(m => m.Name)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize);
            var pageItems = await pageQuery.ToListAsync();
            merged.AddRange(pageItems);
        }
        else
        {
            var items = await queryable
                .OrderBy(m => m.Name)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            merged.AddRange(items);
        }

        return new PagedResultDto<Material>(totalCount, merged);
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
    public async Task<Material> CreateMaterialAsync(string materialName)
    {
        if (string.IsNullOrWhiteSpace(materialName))
        {
            throw new ArgumentException("Material name is required.", nameof(materialName));
        }

        var sessions = await _userSessionRepository.GetListAsync();
        var session = sessions.FirstOrDefault();
        if (session == null)
        {
            throw new BusinessException("AUTH:NO_SESSION", "No active user session found.");
        }

        var response = await _materialPlatformApi.CreateMaterialByNameAsync(
            new CreateMaterialByNameInput(
                materialName.Trim(),
                session.CompanyId,
                session.ProjectId.ToString()));
        if (!response.IsSuccess || response.Data == null)
        {
            var errorMessage = response.Message ?? "Remote material create failed.";
            throw new BusinessException("MATERIAL:REMOTE_CREATE_FAILED", errorMessage);
        }

        var material = MaterialGoodListResultDto.ToEntity(response.Data);

        try
        {
            await UpsertMaterialAsync(material);
        }
        catch (Exception ex)
        {
            throw new BusinessException(
                "MATERIAL:LOCAL_PERSIST_FAILED",
                $"Remote material created but local persist failed: {ex.Message}");
        }

        return material;
    }

    private async Task UpsertMaterialAsync(Material material)
    {
        var existing = await _materialRepository.FindAsync(material.Id);
        if (existing == null)
        {
            await _materialRepository.InsertAsync(material, true);
            return;
        }

        await _materialRepository.UpdateAsync(material, true);
    }
}

