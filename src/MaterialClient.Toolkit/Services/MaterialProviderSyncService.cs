using System.Text.Json;
using MaterialClient.Common.Api;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Toolkit.Services;

public interface IMaterialProviderSyncService
{
    Task SyncAsync(CancellationToken cancellationToken = default);
}

[AutoConstructor]
public partial class MaterialProviderSyncService : IMaterialProviderSyncService, ITransientDependency
{
    private readonly IMaterialPlatformApi _materialPlatformApi;
    private readonly MaterialClientDbContext _dbContext;
    private readonly ILogger<MaterialProviderSyncService> _logger;

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        // ── Phase A: Read local data (no transaction) ──

        var materials = await _dbContext.Materials
            .Where(m => !m.IsDeleted)
            .ToListAsync(cancellationToken);

        var providers = await _dbContext.Providers
            .Where(p => !p.IsDeleted)
            .ToListAsync(cancellationToken);

        if (materials.Count == 0 && providers.Count == 0)
        {
            _logger.LogInformation("No data to sync. Both Materials and Providers tables are empty.");
            return;
        }

        _logger.LogInformation("Phase A: Read {MaterialCount} materials and {ProviderCount} providers from local database.",
            materials.Count, providers.Count);

        // ── Phase A: Push to server (no transaction) ──

        var materialIdMap = new Dictionary<int, int>(); // local Id -> server GoodsId
        var providerIdMap = new Dictionary<int, int>(); // local Id -> server ProviderId
        // Address 为本地专用字段（远端 DTO 不携带）：按服务端 ProviderId 快照本地 Address，重建后回填。
        var providerAddressByServerId = new Dictionary<int, string?>();
        var materialDtos = new List<MaterialGoodListResultDto>();
        var providerDtos = new List<MaterialProviderListResultDto>();

        // Push materials
        foreach (var material in materials)
        {
            if (material.CoId <= 0)
            {
                _logger.LogWarning("Skipping material '{Name}' (Id={Id}): CoId={CoId} is invalid.",
                    material.Name, material.Id, material.CoId);
                continue;
            }

            _logger.LogInformation("Pushing material '{Name}' (Id={Id}, CoId={CoId}) to server...",
                material.Name, material.Id, material.CoId);

            var response = await _materialPlatformApi.CreateMaterialByNameAsync(
                new CreateMaterialByNameInput(material.Name, material.CoId, material.ProId ?? string.Empty),
                cancellationToken);

            if (!response.IsSuccess || response.Data is null)
            {
                _logger.LogError("Failed to create material '{Name}' (Id={Id}) on server. Code={Code}, Message={Message}",
                    material.Name, material.Id, response.Code, response.Message);
                throw new InvalidOperationException(
                    $"Failed to create material '{material.Name}' (Id={material.Id}) on server: {response.Message}");
            }

            materialIdMap[material.Id] = response.Data.GoodsId;
            materialDtos.Add(response.Data);
            _logger.LogInformation("Material '{Name}' created on server with GoodsId={GoodsId}.",
                material.Name, response.Data.GoodsId);
        }

        // Push providers
        foreach (var provider in providers)
        {
            if (provider.CoId <= 0)
            {
                _logger.LogWarning("Skipping provider '{ProviderName}' (Id={Id}): CoId={CoId} is invalid.",
                    provider.ProviderName, provider.Id, provider.CoId);
                continue;
            }

            _logger.LogInformation("Pushing provider '{ProviderName}' (Id={Id}, CoId={CoId}) to server...",
                provider.ProviderName, provider.Id, provider.CoId);

            var response = await _materialPlatformApi.CreateProviderAsync(
                new CreateProviderInput(provider.ProviderName, 0, provider.CoId ?? 0, string.Empty),
                cancellationToken);

            if (!response.IsSuccess || response.Data is null)
            {
                _logger.LogError("Failed to create provider '{ProviderName}' (Id={Id}) on server. Code={Code}, Message={Message}",
                    provider.ProviderName, provider.Id, response.Code, response.Message);
                throw new InvalidOperationException(
                    $"Failed to create provider '{provider.ProviderName}' (Id={provider.Id}) on server: {response.Message}");
            }

            providerIdMap[provider.Id] = response.Data.ProviderId;
            providerAddressByServerId[response.Data.ProviderId] = provider.Address;
            providerDtos.Add(response.Data);
            _logger.LogInformation("Provider '{ProviderName}' created on server with ProviderId={ProviderId}.",
                provider.ProviderName, response.Data.ProviderId);
        }

        _logger.LogInformation("Phase A complete. Pushed {MaterialCount} materials and {ProviderCount} providers.",
            materialDtos.Count, providerDtos.Count);

        // ── Phase B: Replace entities and update FKs (single transaction) ──

        _dbContext.DisableAuditConcepts = true;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            _logger.LogInformation("Phase B: Starting database transaction for entity replacement and FK updates.");

            // Replace Material entities
            _dbContext.Materials.RemoveRange(materials);
            var serverMaterials = materialDtos.Select(MaterialGoodListResultDto.ToEntity).ToList();
            _dbContext.Materials.AddRange(serverMaterials);
            _logger.LogInformation("Replaced {Count} Material entities with server data.", serverMaterials.Count);

            // Replace Provider entities
            _dbContext.Providers.RemoveRange(providers);
            var serverProviders = providerDtos.Select(MaterialProviderListResultDto.ToEntity).ToList();
            // 回填本地 Address（ToEntity 不携带 Address；按服务端 ProviderId=实体 Id 匹配快照）。
            int restoredAddressCount = 0;
            foreach (var serverProvider in serverProviders)
            {
                if (providerAddressByServerId.TryGetValue(serverProvider.Id, out var address) && address != null)
                {
                    serverProvider.Address = address;
                    restoredAddressCount++;
                }
            }
            _dbContext.Providers.AddRange(serverProviders);
            _logger.LogInformation("Replaced {Count} Provider entities with server data (restored {AddressCount} local Address values).",
                serverProviders.Count, restoredAddressCount);

            // Clear MaterialUnit table (references old local MaterialId)
            var materialUnits = await _dbContext.MaterialUnits.ToListAsync(cancellationToken);
            if (materialUnits.Count > 0)
            {
                _dbContext.MaterialUnits.RemoveRange(materialUnits);
                _logger.LogInformation("Cleared {Count} MaterialUnit records (old MaterialId references invalidated).",
                    materialUnits.Count);
            }

            // Clear MaterialType table (local classification data to be re-pulled from server)
            var materialTypes = await _dbContext.MaterialTypes.ToListAsync(cancellationToken);
            if (materialTypes.Count > 0)
            {
                _dbContext.MaterialTypes.RemoveRange(materialTypes);
                _logger.LogInformation("Cleared {Count} MaterialType records (to be re-pulled from server).",
                    materialTypes.Count);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Update WaybillMaterial.MaterialId
            var waybillMaterials = await _dbContext.WaybillMaterials.ToListAsync(cancellationToken);
            int updatedWmCount = 0;
            foreach (var wm in waybillMaterials)
            {
                if (wm.MaterialId != 0 && materialIdMap.TryGetValue(wm.MaterialId, out var newMaterialId))
                {
                    wm.MaterialId = newMaterialId;
                    updatedWmCount++;
                }
            }
            _logger.LogInformation("Updated MaterialId for {Count} WaybillMaterial records.", updatedWmCount);

            // Update Waybill.MaterialId and Waybill.ProviderId
            var waybills = await _dbContext.Waybills.ToListAsync(cancellationToken);
            int updatedWaybillMaterialCount = 0;
            int updatedWaybillProviderCount = 0;
            foreach (var waybill in waybills)
            {
                if (waybill.MaterialId.HasValue && materialIdMap.TryGetValue(waybill.MaterialId.Value, out var newWmId))
                {
                    waybill.MaterialId = newWmId;
                    updatedWaybillMaterialCount++;
                }

                if (waybill.ProviderId.HasValue && providerIdMap.TryGetValue(waybill.ProviderId.Value, out var newWpId))
                {
                    waybill.ProviderId = newWpId;
                    updatedWaybillProviderCount++;
                }
            }
            _logger.LogInformation("Updated MaterialId for {MaterialCount} and ProviderId for {ProviderCount} Waybill records.",
                updatedWaybillMaterialCount, updatedWaybillProviderCount);

            // Update WeighingRecord.ProviderId
            var weighingRecords = await _dbContext.WeighingRecords.ToListAsync(cancellationToken);
            int updatedWrProviderCount = 0;
            foreach (var wr in weighingRecords)
            {
                if (wr.ProviderId.HasValue && providerIdMap.TryGetValue(wr.ProviderId.Value, out var newWrProviderId))
                {
                    wr.ProviderId = newWrProviderId;
                    updatedWrProviderCount++;
                }
            }
            _logger.LogInformation("Updated ProviderId for {Count} WeighingRecord records.", updatedWrProviderCount);

            // Update WeighingRecord.MaterialsJson
            int updatedMaterialsJsonCount = 0;
            foreach (var wr in weighingRecords)
            {
                if (string.IsNullOrEmpty(wr.MaterialsJson))
                    continue;

                try
                {
                    var materialsList = JsonSerializer.Deserialize<List<WeighingRecordMaterial>>(wr.MaterialsJson);
                    if (materialsList is null || materialsList.Count == 0)
                        continue;

                    bool changed = false;
                    foreach (var item in materialsList)
                    {
                        if (item.MaterialId.HasValue && materialIdMap.TryGetValue(item.MaterialId.Value, out var newMId))
                        {
                            item.MaterialId = newMId;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        wr.MaterialsJson = JsonSerializer.Serialize(materialsList);
                        updatedMaterialsJsonCount++;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize MaterialsJson for WeighingRecord (Id={Id}), skipping.",
                        wr.Id);
                }
            }
            _logger.LogInformation("Updated MaterialsJson for {Count} WeighingRecord records.", updatedMaterialsJsonCount);

            await _dbContext.SaveChangesAsync(cancellationToken);

            // ── Validate FK integrity ──

            _logger.LogInformation("Validating FK integrity after sync...");

            // Reload entity IDs for validation
            var validMaterialIds = await _dbContext.Materials.Select(m => m.Id).ToHashSetAsync(cancellationToken);
            var validProviderIds = await _dbContext.Providers.Select(p => p.Id).ToHashSetAsync(cancellationToken);

            // Check WaybillMaterial.MaterialId
            var orphanedWm = waybillMaterials
                .Where(wm => wm.MaterialId != 0 && !validMaterialIds.Contains(wm.MaterialId))
                .ToList();
            foreach (var wm in orphanedWm)
            {
                _logger.LogWarning("Orphaned FK: WaybillMaterial (Id={Id}) has MaterialId={MaterialId} not found in Materials table.",
                    wm.Id, wm.MaterialId);
            }

            // Check Waybill.MaterialId
            var orphanedWaybillMaterial = waybills
                .Where(w => w.MaterialId.HasValue && !validMaterialIds.Contains(w.MaterialId.Value))
                .ToList();
            foreach (var w in orphanedWaybillMaterial)
            {
                _logger.LogWarning("Orphaned FK: Waybill (Id={Id}) has MaterialId={MaterialId} not found in Materials table.",
                    w.Id, w.MaterialId);
            }

            // Check Waybill.ProviderId
            var orphanedWaybillProvider = waybills
                .Where(w => w.ProviderId.HasValue && !validProviderIds.Contains(w.ProviderId.Value))
                .ToList();
            foreach (var w in orphanedWaybillProvider)
            {
                _logger.LogWarning("Orphaned FK: Waybill (Id={Id}) has ProviderId={ProviderId} not found in Providers table.",
                    w.Id, w.ProviderId);
            }

            // Check WeighingRecord.ProviderId
            var orphanedWrProvider = weighingRecords
                .Where(wr => wr.ProviderId.HasValue && !validProviderIds.Contains(wr.ProviderId.Value))
                .ToList();
            foreach (var wr in orphanedWrProvider)
            {
                _logger.LogWarning("Orphaned FK: WeighingRecord (Id={Id}) has ProviderId={ProviderId} not found in Providers table.",
                    wr.Id, wr.ProviderId);
            }

            // Check WeighingRecord.MaterialsJson nested MaterialIds
            foreach (var wr in weighingRecords)
            {
                if (string.IsNullOrEmpty(wr.MaterialsJson))
                    continue;

                try
                {
                    var nestedMaterials = JsonSerializer.Deserialize<List<WeighingRecordMaterial>>(wr.MaterialsJson);
                    if (nestedMaterials is null)
                        continue;

                    foreach (var item in nestedMaterials)
                    {
                        if (item.MaterialId.HasValue && !validMaterialIds.Contains(item.MaterialId.Value))
                        {
                            _logger.LogWarning(
                                "Orphaned FK in MaterialsJson: WeighingRecord (Id={Id}) has nested MaterialId={MaterialId} not found in Materials table.",
                                wr.Id, item.MaterialId);
                        }
                    }
                }
                catch (JsonException)
                {
                    // Already logged during update phase
                }
            }

            int totalOrphans = orphanedWm.Count + orphanedWaybillMaterial.Count +
                               orphanedWaybillProvider.Count + orphanedWrProvider.Count;
            if (totalOrphans == 0)
            {
                _logger.LogInformation("FK integrity validation passed. No orphaned references found.");
            }
            else
            {
                _logger.LogWarning("FK integrity validation found {Count} orphaned references.", totalOrphans);
            }

            // Commit transaction
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Phase B complete. Sync finished successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Phase B failed. Rolling back transaction...");
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            _dbContext.DisableAuditConcepts = false;
        }
    }
}
