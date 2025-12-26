using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

public interface ISyncMaterialService
{
    Task SyncMaterialAsync();

    Task SyncMaterialTypeAsync();
    Task SyncProviderAsync();
}

[AutoConstructor]
public partial class SyncMaterialService : DomainService, ISyncMaterialService
{
    private readonly IRepository<LicenseInfo, Guid> _licenseInfoRepository;
    private readonly ILogger<SyncMaterialService> _logger;
    private readonly IMaterialPlatformApi _materialPlatformApi;

    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<MaterialType, int> _materialTypeRepository;
    private readonly IRepository<MaterialUnit, int> _materialUnitRepository;
    private readonly IRepository<Provider, int> _providerRepository;
    private readonly IRepository<WorkSettingsEntity, int> _workSettingRepository;


    [UnitOfWork]
    public async Task SyncMaterialAsync()
    {
        try
        {
            var now = DateTime.Now;
            var licenseInfo = await _licenseInfoRepository.FirstOrDefaultAsync();
            if (licenseInfo == null)
            {
                _logger.LogWarning("未找到许可证信息，跳过物料同步");
                return;
            }

            var workSetting = await _workSettingRepository.FirstOrDefaultAsync();
            long timestamp = 0;

            if (workSetting?.MaterialUpdatedTime != null)
                timestamp = new DateTimeOffset(workSetting.MaterialUpdatedTime.Value).ToUnixTimeSeconds();

            var request = new GetMaterialGoodListInput(
                licenseInfo.ProjectId.ToString(),
                timestamp
            );

            _logger.LogInformation("开始获取物料数据，项目ID: {ProjectId}, 时间戳: {Timestamp}",
                licenseInfo.ProjectId, timestamp);

            List<MaterialGoodListResultDto>? materialList;
            try
            {
                materialList = await _materialPlatformApi.GetMaterialGoodListAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调用物料平台API时发生异常，项目ID: {ProjectId}", licenseInfo.ProjectId);
                return;
            }

            if (materialList.Count == 0)
            {
                _logger.LogInformation("没有需要同步的物料数据");
                return;
            }

            var list = materialList.Select(MaterialGoodListResultDto.ToEntity).ToList();

            var q = await _materialRepository.GetQueryableAsync();

            var existingMaterialIds = await q
                .Select(x => x.Id)
                .Where(x => list.Select(l => l.Id).Contains(x))
                .ToListAsync();

            var materialsToUpdate = list.Where(x => existingMaterialIds.Contains(x.Id)).ToList();
            var materialsToInsert = list.Where(x => !existingMaterialIds.Contains(x.Id)).ToList();

            if (materialsToUpdate.Count > 0) await _materialRepository.UpdateManyAsync(materialsToUpdate);

            if (materialsToInsert.Count > 0) await _materialRepository.InsertManyAsync(materialsToInsert);

            _logger.LogInformation("物料同步完成，更新 {UpdateCount} 条，新增 {InsertCount} 条",
                materialsToUpdate.Count, materialsToInsert.Count);

            // 同步物料单位
            await SyncMaterialUnitsAsync(materialList);

            if (workSetting != null)
            {
                workSetting.MaterialUpdatedTime = now;
                await _workSettingRepository.UpdateAsync(workSetting);
            }
            else
            {
                workSetting = new WorkSettingsEntity
                {
                    MaterialUpdatedTime = now
                };
                await _workSettingRepository.InsertAsync(workSetting);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步物料数据时发生异常");
            throw;
        }
    }

    [UnitOfWork]
    public async Task SyncMaterialTypeAsync()
    {
        try
        {
            var now = DateTime.Now;
            var licenseInfo = await _licenseInfoRepository.FirstOrDefaultAsync();
            if (licenseInfo == null)
            {
                _logger.LogWarning("未找到许可证信息，跳过物料类型同步");
                return;
            }

            var workSetting = await _workSettingRepository.FirstOrDefaultAsync();
            long timestamp = 0;

            if (workSetting?.MaterialTypeUpdatedTime != null)
                timestamp = new DateTimeOffset(workSetting.MaterialTypeUpdatedTime.Value).ToUnixTimeSeconds();

            var request = new GetMaterialGoodTypeListInput(
                licenseInfo.ProjectId.ToString(),
                timestamp
            );

            _logger.LogInformation("开始获取物料类型数据，项目ID: {ProjectId}, 时间戳: {Timestamp}",
                licenseInfo.ProjectId, timestamp);

            List<MaterialGoodTypeListResultDto>? materialTypeList;
            try
            {
                materialTypeList = await _materialPlatformApi.MaterialGoodTypeListAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调用物料类型平台API时发生异常，项目ID: {ProjectId}", licenseInfo.ProjectId);
                return;
            }

            if (materialTypeList.Count == 0)
            {
                _logger.LogInformation("没有需要同步的物料类型数据");
                return;
            }

            var list = materialTypeList.Select(MaterialGoodTypeListResultDto.ToEntity).ToList();

            var q = await _materialTypeRepository.GetQueryableAsync();

            var existingTypeIds = await q
                .Select(x => x.Id)
                .Where(x => list.Select(l => l.Id).Contains(x))
                .ToListAsync();

            var typesToUpdate = list.Where(x => existingTypeIds.Contains(x.Id)).ToList();
            var typesToInsert = list.Where(x => !existingTypeIds.Contains(x.Id)).ToList();

            if (typesToUpdate.Count > 0) await _materialTypeRepository.UpdateManyAsync(typesToUpdate);

            if (typesToInsert.Count > 0) await _materialTypeRepository.InsertManyAsync(typesToInsert);

            _logger.LogInformation("物料类型同步完成，更新 {UpdateCount} 条，新增 {InsertCount} 条",
                typesToUpdate.Count, typesToInsert.Count);

            if (workSetting != null)
            {
                workSetting.MaterialTypeUpdatedTime = now;
                await _workSettingRepository.UpdateAsync(workSetting);
            }
            else
            {
                workSetting = new WorkSettingsEntity
                {
                    MaterialTypeUpdatedTime = now
                };
                await _workSettingRepository.InsertAsync(workSetting);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步物料类型数据时发生异常");
            throw;
        }
    }

    [UnitOfWork]
    public async Task SyncProviderAsync()
    {
        try
        {
            var now = DateTime.Now;
            var licenseInfo = await _licenseInfoRepository.FirstOrDefaultAsync();
            if (licenseInfo == null)
            {
                _logger.LogWarning("未找到许可证信息，跳过供应商同步");
                return;
            }

            var workSetting = await _workSettingRepository.FirstOrDefaultAsync();
            long timestamp = 0;

            if (workSetting?.ProviderUpdatedTime != null)
                timestamp = new DateTimeOffset(workSetting.ProviderUpdatedTime.Value).ToUnixTimeSeconds();

            var request = new GetMaterialProviderListInput(
                licenseInfo.ProjectId.ToString(),
                timestamp
            );

            _logger.LogInformation("开始获取供应商数据，项目ID: {ProjectId}, 时间戳: {Timestamp}",
                licenseInfo.ProjectId, timestamp);

            List<MaterialProviderListResultDto>? providerList;
            try
            {
                providerList = await _materialPlatformApi.MaterialProviderListAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调用供应商平台API时发生异常，项目ID: {ProjectId}", licenseInfo.ProjectId);
                return;
            }

            if (providerList.Count == 0)
            {
                _logger.LogInformation("没有需要同步的供应商数据");
                return;
            }

            var list = providerList.Select(MaterialProviderListResultDto.ToEntity).ToList();

            var q = await _providerRepository.GetQueryableAsync();

            var existingProviderIds = await q
                .Select(x => x.Id)
                .Where(x => list.Select(l => l.Id).Contains(x))
                .ToListAsync();

            var providersToUpdate = list.Where(x => existingProviderIds.Contains(x.Id)).ToList();
            var providersToInsert = list.Where(x => !existingProviderIds.Contains(x.Id)).ToList();

            if (providersToUpdate.Count > 0) await _providerRepository.UpdateManyAsync(providersToUpdate);

            if (providersToInsert.Count > 0) await _providerRepository.InsertManyAsync(providersToInsert);

            _logger.LogInformation("供应商同步完成，更新 {UpdateCount} 条，新增 {InsertCount} 条",
                providersToUpdate.Count, providersToInsert.Count);

            if (workSetting != null)
            {
                workSetting.ProviderUpdatedTime = now;
                await _workSettingRepository.UpdateAsync(workSetting);
            }
            else
            {
                workSetting = new WorkSettingsEntity
                {
                    ProviderUpdatedTime = now
                };
                await _workSettingRepository.InsertAsync(workSetting);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步供应商数据时发生异常");
            throw;
        }
    }

    /// <summary>
    ///     同步物料单位
    /// </summary>
    private async Task SyncMaterialUnitsAsync(List<MaterialGoodListResultDto> materialList)
    {
        try
        {
            // 收集所有需要同步的物料单位
            var allUnits = new List<MaterialUnit>();
            foreach (var material in materialList)
                if (material.Units?.Count > 0)
                {
                    var units = material.Units
                        .Select(u => MaterialGoodUnitResultDto.ToEntity(u, material.GoodsId))
                        .ToList();
                    allUnits.AddRange(units);
                }

            if (allUnits.Count == 0)
            {
                _logger.LogInformation("没有需要同步的物料单位数据");
                return;
            }

            var unitQuery = await _materialUnitRepository.GetQueryableAsync();

            // 查询已存在的物料单位ID
            var existingUnitIds = await unitQuery
                .Where(u => allUnits.Select(au => au.Id).Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync();

            var unitsToUpdate = allUnits.Where(u => existingUnitIds.Contains(u.Id)).ToList();
            var unitsToInsert = allUnits.Where(u => !existingUnitIds.Contains(u.Id)).ToList();

            if (unitsToUpdate.Count > 0) await _materialUnitRepository.UpdateManyAsync(unitsToUpdate);

            if (unitsToInsert.Count > 0) await _materialUnitRepository.InsertManyAsync(unitsToInsert);

            _logger.LogInformation("物料单位同步完成，更新 {UpdateCount} 条，新增 {InsertCount} 条",
                unitsToUpdate.Count, unitsToInsert.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步物料单位时发生异常");
            throw;
        }
    }
}