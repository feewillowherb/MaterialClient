using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Common.Services.Urban;
using MaterialClient.Urban.Api;
using MaterialClient.Urban.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Urban.Services;

public interface IUrbanPassageUploadService : ITransientDependency
{
    Task<bool> SubmitPassageRecordAsync(Guid passageRecordId);
}

[AutoConstructor]
public partial class UrbanPassageUploadService : IUrbanPassageUploadService
{
    private readonly IUrbanManagementApi _urbanManagementApi;
    private readonly IUrbanAttachmentSyncService _attachmentSyncService;
    private readonly IUrbanPassageRecordService _passageRecordService;
    private readonly IRepository<UrbanPassageRecord, Guid> _passageRepository;
    private readonly ILicenseService _licenseService;
    private readonly IMachineCodeService _machineCodeService;
    private readonly ILogger<UrbanPassageUploadService> _logger;

    [UnitOfWork]
    public async Task<bool> SubmitPassageRecordAsync(Guid passageRecordId)
    {
        try
        {
            var record = await _passageRepository.GetAsync(passageRecordId);
            if (record.SyncStatus == SyncStatus.Synced)
            {
                return true;
            }

            var licenseInfo = await _licenseService.GetCurrentLicenseAsync();
            if (licenseInfo == null)
            {
                _logger.LogWarning(
                    "LicenseInfo not available; cannot upload passage record {PassageRecordId}",
                    passageRecordId);
                await _passageRecordService.MarkUploadFailedAsync(passageRecordId);
                return false;
            }

            if (licenseInfo.ProjectId == Guid.Empty)
            {
                _logger.LogWarning(
                    "LicenseInfo.ProjectId is empty; cannot upload passage record {PassageRecordId}",
                    passageRecordId);
                await _passageRecordService.MarkUploadFailedAsync(passageRecordId);
                return false;
            }

            var submitMachineCode = _machineCodeService.GetMachineCode();
            record.AssignSubmitMachineCode(submitMachineCode);
            await _passageRepository.UpdateAsync(record, autoSave: true);

            var accessCode = licenseInfo.AccessCode ?? string.Empty;
            var attachmentIds = (await _attachmentSyncService.UploadPassageAttachmentsAsync(
                record.LargeImageAttachmentId,
                record.SmallImageAttachmentId,
                accessCode)).ToList();

            var dto = UrbanPassageSubmitDto.FromPassage(record, licenseInfo, submitMachineCode, attachmentIds);
            UrbanPassageReceiveResult response = record.PassageSource switch
            {
                PassageSource.Checkpoint => await _urbanManagementApi.ReceiveCheckpointPassageAsync(dto),
                PassageSource.FinishedProduct => await _urbanManagementApi.ReceiveFinishedProductPassageAsync(dto),
                _ => throw new InvalidOperationException($"Unsupported passage source: {record.PassageSource}")
            };

            if (response.RecordId == Guid.Empty)
            {
                _logger.LogWarning(
                    "Passage record {PassageRecordId} upload returned empty server id",
                    passageRecordId);
                await _passageRecordService.MarkUploadFailedAsync(passageRecordId);
                return false;
            }

            await _passageRecordService.MarkSyncedAsync(passageRecordId);
            _logger.LogInformation(
                "Passage record {PassageRecordId} uploaded. ServerId={ServerId}, AttachmentCount={AttachmentCount}",
                passageRecordId,
                response.RecordId,
                attachmentIds.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload passage record {PassageRecordId}", passageRecordId);
            await _passageRecordService.MarkUploadFailedAsync(passageRecordId);
            return false;
        }
    }
}
