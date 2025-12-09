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
    private readonly IRepository<WorkSetting, int> _workSettingRepository;
    private readonly IRepository<LicenseInfo, Guid> _licenseInfoRepository;
    private readonly ILogger<SyncMaterialService> _logger;


    [UnitOfWork]
    public async Task SyncMaterialAsync()
    {
        var licenseInfo = await _licenseInfoRepository.FirstOrDefaultAsync();
        if (licenseInfo == null)
        {
            _logger.LogWarning("License info not found, skipping material sync.");
            return;
        }

        var workSetting = await _workSettingRepository.FirstOrDefaultAsync();
        long timestamp = 0;

        if (workSetting?.MaterialUpdateTime != null)
        {
            timestamp = workSetting.MaterialUpdateTime.Value.Ticks;
        }

        var request = new GetMaterialGoodListInput(
            ProId: licenseInfo.ProjectId.ToString(),
            UpdateTime: timestamp
        );

        var materialResult = await _materialPlatformApi.GetMaterialGoodListAsync(request);
        if (materialResult.Success == false)
        {
            _logger.LogError("Failed to fetch material error message: {ErrorMessage}, code: {Code}", materialResult.Msg,
                materialResult.Code);
            return;
        }

        var list = materialResult.Data.Select(MaterialGoodListResultDto.ToEntity).ToList();

        var q = await _materialRepository.GetQueryableAsync();

        var existingMaterialIds = await q
            .Select(x => x.Id)
            .Where(x => list.Select(l => l.Id).Contains(x))
            .ToListAsync();
        var materialsToUpdate = list.Where(x => existingMaterialIds.Contains(x.Id)).ToList();
        var materialsToInsert = list.Where(x => !existingMaterialIds.Contains(x.Id)).ToList();
        await _materialRepository.UpdateManyAsync(materialsToUpdate);
        await _materialRepository.InsertManyAsync(materialsToInsert);
    }
}