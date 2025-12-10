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
}

[AutoConstructor]
public partial class SyncMaterialService : DomainService, ISyncMaterialService
{
    private readonly IMaterialPlatformApi _materialPlatformApi;

    private readonly IRepository<Material, int> _materialRepository;
    private readonly IRepository<WorkSettingsEntity, int> _workSettingRepository;
    private readonly IRepository<LicenseInfo, Guid> _licenseInfoRepository;
    private readonly ILogger<SyncMaterialService> _logger;


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

            if (workSetting?.MaterialUpdateTime != null)
            {
                timestamp = new DateTimeOffset(workSetting.MaterialUpdateTime.Value).ToUnixTimeSeconds();
            }

            var request = new GetMaterialGoodListInput(
                ProId: licenseInfo.ProjectId.ToString(),
                UpdateTime: timestamp
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
            
            if (materialsToUpdate.Count > 0)
            {
                await _materialRepository.UpdateManyAsync(materialsToUpdate);
            }
            
            if (materialsToInsert.Count > 0)
            {
                await _materialRepository.InsertManyAsync(materialsToInsert);
            }

            _logger.LogInformation("物料同步完成，更新 {UpdateCount} 条，新增 {InsertCount} 条",
                materialsToUpdate.Count, materialsToInsert.Count);

            if (workSetting != null)
            {
                workSetting.MaterialUpdateTime = now;
                await _workSettingRepository.UpdateAsync(workSetting);
            }
            else
            {
                workSetting = new WorkSettingsEntity
                {
                    MaterialUpdateTime = now
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
}