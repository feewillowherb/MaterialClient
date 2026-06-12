using System;
using System.Linq;
using System.Text.Json;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Common.Services.Urban;
using MaterialClient.Common.Utils;
using MaterialClient.Urban.Api;
using MaterialClient.Urban.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

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
    private readonly IUrbanAttachmentSyncService _attachmentSyncService;
    private readonly IAttachmentService _attachmentService;
    private readonly IRepository<WeighingRecord, long> _weighingRecordRepository;
    private readonly IUrbanWeighingExtensionService _extensionService;
    private readonly ILicenseService _licenseService;
    private readonly ILogger<UrbanServerUploadService> _logger;

    public UrbanServerUploadService(
        IUrbanManagementApi urbanManagementApi,
        IUrbanAttachmentSyncService attachmentSyncService,
        IAttachmentService attachmentService,
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IUrbanWeighingExtensionService extensionService,
        ILicenseService licenseService,
        ILogger<UrbanServerUploadService> logger)
    {
        _urbanManagementApi = urbanManagementApi;
        _attachmentSyncService = attachmentSyncService;
        _attachmentService = attachmentService;
        _weighingRecordRepository = weighingRecordRepository;
        _extensionService = extensionService;
        _licenseService = licenseService;
        _logger = logger;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task SubmitRecordAsync(long weighingRecordId)
    {
        try
        {
            var record = await _weighingRecordRepository.GetAsync(weighingRecordId);
            var extension = await _extensionService.GetByWeighingRecordIdAsync(weighingRecordId);

            var licenseInfo = await _licenseService.GetCurrentLicenseAsync();

            if (licenseInfo == null)
            {
                _logger.LogWarning(
                    "LicenseInfo not available, ProId/ProName/BuildLicenseNo/FdBuildLicenseNo will be null for record {RecordId}",
                    weighingRecordId);
            }
            else if (licenseInfo.ProName == null || licenseInfo.BuildLicenseNo == null)
            {
                _logger.LogDebug(
                    "LicenseInfo exists but some project fields are empty for record {RecordId}",
                    weighingRecordId);
            }

            var buildLicenseNo = licenseInfo?.BuildLicenseNo ?? string.Empty;
            var attachmentIds =
                (await _attachmentSyncService.UploadAttachmentsAsync(weighingRecordId, buildLicenseNo)).ToList();

            var hadLocalUrbanAttachments = await HasLocalUrbanAttachmentsAsync(weighingRecordId);
            if (hadLocalUrbanAttachments && attachmentIds.Count == 0)
            {
                _logger.LogWarning(
                    "Record {RecordId} has local Lrp/UrbanPhoto attachments but none were uploaded; keeping Pending for retry",
                    weighingRecordId);
                return;
            }

            var dto = new UrbanWeighingRecordSubmitDto
            {
                ClientRecordId = record.Id,
                PlateNumber = record.PlateNumber,
                TotalWeight = MaterialMath.ConvertTonToKg(record.TotalWeight),
                WeighingTime = record.AddDate,
                SyncType = 0,
                VehicleColor = null,
                PlateColor = null,
                VehicleType = null,
                DeviceId = null,
                BuildLicenseNo = licenseInfo?.BuildLicenseNo,
                FdBuildLicenseNo = licenseInfo?.FdBuildLicenseNo,
                SiteType = null,
                ProId = licenseInfo?.ProjectId.ToString(),
                ProName = licenseInfo?.ProName,
                IsAnomaly = extension?.IsAnomaly ?? false,
                AnomalyReason = extension?.AnomalyReason,
                ClientSyncType = (int?)(extension?.SyncStatus ?? SyncStatus.Pending),
                ClientSyncTime = null,
                ClientRetryCount = extension?.RetryCount,
                ClientLastErrorTime = extension?.LastErrorTime,
                AttachmentIds = attachmentIds.Count > 0 ? attachmentIds : null
            };

            // Build ExtraProperties with edit history from extension
            if (extension != null)
            {
                var editHistory = extension.GetEditHistory();
                if (editHistory.Count > 0)
                {
                    dto.ExtraProperties = new Dictionary<string, object?>
                    {
                        ["EditHistory"] = JsonSerializer.Serialize(editHistory)
                    };
                }
            }

            var response = await _urbanManagementApi.ReceiveWeighingRecordAsync(dto);

            if (response.RecordId > 0)
            {
                if (extension != null)
                {
                    await _extensionService.UpdateSyncStatusAsync(extension.Id, SyncStatus.Synced);
                }

                _logger.LogInformation(
                    "Record {RecordId} submitted to server successfully. ServerId={ServerId}, AttachmentCount={AttachmentCount}",
                    weighingRecordId,
                    response.RecordId,
                    attachmentIds.Count);
            }
            else
            {
                _logger.LogWarning(
                    "Record {RecordId} submission returned invalid server record id",
                    weighingRecordId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit record {RecordId} to server", weighingRecordId);
        }
    }

    private async Task<bool> HasLocalUrbanAttachmentsAsync(long weighingRecordId)
    {
        var map = await _attachmentService.GetAttachmentsByWeighingRecordIdsAsync([weighingRecordId]);
        if (!map.TryGetValue(weighingRecordId, out var files) || files.Count == 0)
        {
            return false;
        }

        return files.Exists(f => f.AttachType is AttachType.Lrp or AttachType.UrbanPhoto);
    }
}
