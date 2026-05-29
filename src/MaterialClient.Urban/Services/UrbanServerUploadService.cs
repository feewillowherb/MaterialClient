using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using MaterialClient.Common.Services.Urban;
using MaterialClient.Urban.Api;
using MaterialClient.Urban.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MaterialClient.Urban.Services;

/// <summary>
///     称重记录服务端上传服务
///     在本地保存后调用 UrbanManagement 服务端 API 提交记录
/// </summary>
public interface IUrbanServerUploadService : ITransientDependency
{
    /// <summary>
    ///     将称重记录提交到 UrbanManagement 服务端
    /// </summary>
    Task SubmitRecordAsync(long weighingRecordId);
}

/// <inheritdoc />
public class UrbanServerUploadService : IUrbanServerUploadService
{
    private readonly IUrbanManagementApi _urbanManagementApi;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IUrbanWeighingExtensionService _extensionService;
    private readonly IRepository<WeighingRecordAttachment, int> _attachmentRepository;
    private readonly ILogger<UrbanServerUploadService> _logger;

    public UrbanServerUploadService(
        IUrbanManagementApi urbanManagementApi,
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IUrbanWeighingExtensionService extensionService,
        IRepository<WeighingRecordAttachment, int> attachmentRepository,
        ILogger<UrbanServerUploadService> logger)
    {
        _urbanManagementApi = urbanManagementApi;
        _weighingRecordRepository = weighingRecordRepository;
        _extensionService = extensionService;
        _attachmentRepository = attachmentRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SubmitRecordAsync(long weighingRecordId)
    {
        try
        {
            var record = await _weighingRecordRepository.GetAsync(weighingRecordId);
            var extension = await _extensionService.GetByWeighingRecordIdAsync(weighingRecordId);

            // Get attachment file IDs (local int IDs; server uses Guid)
            var attachmentQueryable = await _attachmentRepository.GetQueryableAsync();
            var localAttachmentIds = await attachmentQueryable
                .Where(a => a.WeighingRecordId == weighingRecordId)
                .Select(a => a.AttachmentFileId)
                .ToListAsync();

            var dto = new UrbanWeighingRecordSubmitDto
            {
                ClientRecordId = record.Id,
                PlateNumber = record.PlateNumber,
                TotalWeight = record.TotalWeight,
                WeighingTime = record.AddDate,
                SyncType = 0,
                VehicleColor = null,
                PlateColor = null,
                VehicleType = null,
                DeviceId = null,
                BuildLicenseNo = null,
                SiteType = null,
                ProId = null,
                ProName = null,
                IsAnomaly = extension?.IsAnomaly ?? false,
                ClientSyncType = (int?)(extension?.SyncStatus ?? SyncStatus.Pending),
                ClientSyncTime = null,
                ClientRetryCount = extension?.RetryCount,
                ClientLastErrorTime = extension?.LastErrorTime,
                AttachmentIds = null // Server-side attachments created separately via FileService
            };

            var response = await _urbanManagementApi.SubmitWeighingRecordAsync(dto);

            if (response.Success)
            {
                // Update local extension to synced
                if (extension != null)
                {
                    await _extensionService.UpdateSyncStatusAsync(extension.Id, SyncStatus.Synced);
                }

                _logger.LogInformation("Record {RecordId} submitted to server successfully. ServerId={ServerId}",
                    weighingRecordId, response.Data?.Id);
            }
            else
            {
                _logger.LogWarning("Record {RecordId} submission failed: {Msg}",
                    weighingRecordId, response.Msg);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit record {RecordId} to server", weighingRecordId);
        }
    }
}
