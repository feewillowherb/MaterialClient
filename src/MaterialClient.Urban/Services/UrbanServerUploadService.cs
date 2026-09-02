using System;
using System.Linq;
using System.Text.Json;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using MaterialClient.Common.Extensions;
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
    /// <returns>提交成功返回 true；失败时保留 Pending 状态并返回 false</returns>
    Task<bool> SubmitRecordAsync(long weighingRecordId);
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
    private readonly IMachineCodeService _machineCodeService;
    private readonly ILogger<UrbanServerUploadService> _logger;

    public UrbanServerUploadService(
        IUrbanManagementApi urbanManagementApi,
        IUrbanAttachmentSyncService attachmentSyncService,
        IAttachmentService attachmentService,
        IRepository<WeighingRecord, long> weighingRecordRepository,
        IUrbanWeighingExtensionService extensionService,
        ILicenseService licenseService,
        IMachineCodeService machineCodeService,
        ILogger<UrbanServerUploadService> logger)
    {
        _urbanManagementApi = urbanManagementApi;
        _attachmentSyncService = attachmentSyncService;
        _attachmentService = attachmentService;
        _weighingRecordRepository = weighingRecordRepository;
        _extensionService = extensionService;
        _licenseService = licenseService;
        _machineCodeService = machineCodeService;
        _logger = logger;
    }

    /// <inheritdoc />
    [UnitOfWork]
    public async Task<bool> SubmitRecordAsync(long weighingRecordId)
    {
        try
        {
            var record = await _weighingRecordRepository.GetAsync(weighingRecordId);
            var extension = await _extensionService.GetByWeighingRecordIdAsync(weighingRecordId);

            if (extension == null)
            {
                _logger.LogWarning(
                    "No UrbanWeighingExtension for record {RecordId}; cannot submit without extension Id",
                    weighingRecordId);
                return false;
            }

            var licenseInfo = await _licenseService.GetCurrentLicenseAsync();

            if (licenseInfo == null)
            {
                _logger.LogWarning(
                    "LicenseInfo not available; cannot submit weighing record {RecordId}",
                    weighingRecordId);
                return false;
            }

            if (licenseInfo.ProjectId == Guid.Empty)
            {
                _logger.LogWarning(
                    "LicenseInfo.ProjectId is empty; cannot submit weighing record {RecordId}",
                    weighingRecordId);
                return false;
            }

            if (licenseInfo.ProName == null || licenseInfo.AccessCode == null)
            {
                _logger.LogDebug(
                    "LicenseInfo exists but some project fields are empty for record {RecordId}",
                    weighingRecordId);
            }

            // F2: record the machine code that submits this weighing data (traceability only —
            // MUST NOT be used for authorization; that is F4's responsibility).
            var submitMachineCode = _machineCodeService.GetMachineCode();
            // Tracked entity within the ambient UnitOfWork → persisted on save.
            extension.SubmitMachineCode = submitMachineCode;

            var accessCode = licenseInfo.AccessCode ?? string.Empty;
            var editHistory = extension.GetEditHistory();
            var skipAttachmentUpload = editHistory.Count > 0
                                       && !editHistory.Any(e => e.IsImagesModified);
            List<Guid> attachmentIds;
            if (skipAttachmentUpload)
            {
                attachmentIds = [];
                _logger.LogDebug(
                    "Skipping attachment upload for record {RecordId} (re-transmit after client edit)",
                    weighingRecordId);
            }
            else
            {
                attachmentIds =
                    (await _attachmentSyncService.UploadAttachmentsAsync(weighingRecordId, accessCode)).ToList();
            }

            var hadLocalUrbanAttachments = await HasLocalUrbanAttachmentsAsync(weighingRecordId);
            if (!skipAttachmentUpload && hadLocalUrbanAttachments && attachmentIds.Count == 0)
            {
                _logger.LogWarning(
                    "Record {RecordId} has local Lpr/UrbanPhoto attachments but none were uploaded; keeping Pending for retry",
                    weighingRecordId);
                return false;
            }

            var isAnomaly = extension.IsAnomaly;

            var dto = new UrbanWeighingRecordSubmitDto
            {
                ClientRecordId = extension.Id,
                PlateNumber = record.PlateNumber,
                TotalWeight = MaterialMath.ConvertTonToKg(record.TotalWeight),
                WeighingTime = record.AddDate,
                SyncType = isAnomaly ? null : 0,
                VehicleColor = null,
                PlateColor = null,
                VehicleType = null,
                DeviceId = null,
                BuildLicenseNo = licenseInfo.AccessCode,
                SiteType = null,
                ProId = licenseInfo.ProjectId,
                ProName = licenseInfo.ProName,
                SubmitMachineCode = submitMachineCode,
                IsAnomaly = isAnomaly,
                AnomalyReason = extension.AnomalyReason?.GetDescription(),
                ClientSyncType = (int?)extension.SyncStatus,
                ClientSyncTime = null,
                ClientRetryCount = extension.RetryCount,
                ClientLastErrorTime = extension.LastErrorTime,
                AttachmentIds = attachmentIds.Count > 0 ? attachmentIds : null
            };

            // Build ExtraProperties with edit history from extension
            var normalizedEditHistory = editHistory.NormalizeWeightsForServer();
            if (normalizedEditHistory.Count > 0)
            {
                dto.ExtraProperties = new Dictionary<string, object?>
                {
                    ["EditHistory"] = JsonSerializer.Serialize(normalizedEditHistory)
                };
            }

            var response = await _urbanManagementApi.ReceiveWeighingRecordAsync(dto);

            if (response.RecordId != Guid.Empty)
            {
                await _extensionService.UpdateSyncStatusAsync(extension.Id, SyncStatus.Synced);

                _logger.LogInformation(
                    "Record {RecordId} submitted to server successfully. ServerId={ServerId}, AttachmentCount={AttachmentCount}",
                    weighingRecordId,
                    response.RecordId,
                    attachmentIds.Count);
                return true;
            }

            _logger.LogWarning(
                "Record {RecordId} submission returned invalid server record id",
                weighingRecordId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit record {RecordId} to server", weighingRecordId);
            return false;
        }
    }

    private async Task<bool> HasLocalUrbanAttachmentsAsync(long weighingRecordId)
    {
        var map = await _attachmentService.GetAttachmentsByWeighingRecordIdsAsync([weighingRecordId]);
        if (!map.TryGetValue(weighingRecordId, out var files) || files.Count == 0)
        {
            return false;
        }

        return files.Exists(f => f.AttachType is AttachType.Lpr or AttachType.UrbanPhoto);
    }
}
