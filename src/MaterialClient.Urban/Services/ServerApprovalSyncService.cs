using MaterialClient.Common.Models;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services.Urban;
using MaterialClient.Common.Utils;
using MaterialClient.Urban.Api;
using MaterialClient.Urban.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;

namespace MaterialClient.Urban.Services;

public interface IServerApprovalSyncService : ITransientDependency
{
    Task<bool> ApplyServerApprovalAsync(WeighingRecordApprovedPushDto push);
}

[AutoConstructor]
public partial class ServerApprovalSyncService : IServerApprovalSyncService
{
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IUrbanWeighingExtensionService _urbanWeighingExtensionService;
    private readonly IUrbanManagementApi _urbanManagementApi;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<ServerApprovalSyncService> _logger;

    [UnitOfWork]
    public virtual async Task<bool> ApplyServerApprovalAsync(WeighingRecordApprovedPushDto push)
    {
        var record = await _weighingRecordRepository.FindAsync(push.ClientRecordId);
        if (record == null)
        {
            _logger.LogWarning(
                "Server approval sync skipped: local record {ClientRecordId} not found",
                push.ClientRecordId);
            return false;
        }

        var totalWeightTon = MaterialMath.ConvertKgToTon(push.TotalWeight);
        var extension = await _urbanWeighingExtensionService.GetByWeighingRecordIdAsync(push.ClientRecordId);

        var alreadyApplied = extension != null
                             && !extension.IsAnomaly
                             && string.Equals(record.PlateNumber, push.PlateNumber, StringComparison.Ordinal)
                             && record.TotalWeight == totalWeightTon;

        if (!alreadyApplied)
        {
            record.PlateNumber = push.PlateNumber;
            record.TotalWeight = totalWeightTon;
            await _weighingRecordRepository.UpdateAsync(record, autoSave: true);

            if (extension != null)
            {
                await _urbanWeighingExtensionService.UpdateSyncStatusAsync(extension.Id, SyncStatus.Synced);
                await _urbanWeighingExtensionService.UpdateAnomalyStateAsync(extension.Id, false, null);
            }
        }

        try
        {
            await _urbanManagementApi.AckApprovalSyncAsync(
                new AckApprovalSyncDto { ClientRecordId = push.ClientRecordId });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "ACK for server approval sync failed for ClientRecordId={ClientRecordId}; local state kept",
                push.ClientRecordId);
        }

        await _localEventBus.PublishAsync(new ServerApprovalSyncedEventData(push.ClientRecordId));
        return true;
    }
}
