using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using MaterialClient.Common.Utils;
using MaterialClient.Urban.Api;
using MaterialClient.Urban.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Services;

/// <summary>
///     Uploads local weighing attachments to UrbanManagement before record receive.
/// </summary>
public interface IUrbanAttachmentSyncService : ITransientDependency
{
    /// <summary>
    ///     Upload Lrp and UrbanPhoto files for a weighing record; returns server attachment Guids.
    /// </summary>
    Task<IReadOnlyList<Guid>> UploadAttachmentsAsync(long weighingRecordId, string buildLicenseNo);
}

/// <inheritdoc />
[AutoConstructor]
public partial class UrbanAttachmentSyncService : IUrbanAttachmentSyncService
{
    private const string UnknownBuildLicenseNo = "unknown";

    private static readonly AttachType[] UrbanSyncAttachTypes = [AttachType.Lpr, AttachType.UrbanPhoto];

    private readonly IAttachmentService _attachmentService;
    private readonly IUrbanManagementApi _urbanManagementApi;
    private readonly ILogger<UrbanAttachmentSyncService> _logger;

    public async Task<IReadOnlyList<Guid>> UploadAttachmentsAsync(long weighingRecordId, string buildLicenseNo)
    {
        var licenseNo = string.IsNullOrWhiteSpace(buildLicenseNo) ? UnknownBuildLicenseNo : buildLicenseNo.Trim();
        if (licenseNo == UnknownBuildLicenseNo)
        {
            _logger.LogWarning(
                "BuildLicenseNo missing for record {RecordId}; using placeholder for attachment upload path",
                weighingRecordId);
        }

        var attachmentsByRecord =
            await _attachmentService.GetAttachmentsByWeighingRecordIdsAsync([weighingRecordId]);

        if (!attachmentsByRecord.TryGetValue(weighingRecordId, out var attachments) || attachments.Count == 0)
        {
            return [];
        }

        var urbanAttachments = attachments
            .Where(a => UrbanSyncAttachTypes.Contains(a.AttachType))
            .ToList();

        if (urbanAttachments.Count == 0)
        {
            return [];
        }

        var serverIds = new List<Guid>();
        var expectedCount = 0;
        var readSuccessCount = 0;

        foreach (var group in urbanAttachments.GroupBy(a => a.AttachType))
        {
            var base64Images = new List<string>();
            foreach (var attachment in group)
            {
                expectedCount++;
                var absolutePath = PathManager.ToAbsolutePath(attachment.LocalPath);
                if (!File.Exists(absolutePath))
                {
                    _logger.LogWarning(
                        "Attachment file missing for record {RecordId}, FileId={FileId}, Path={Path}",
                        weighingRecordId,
                        attachment.Id,
                        attachment.LocalPath);
                    continue;
                }

                var bytes = await File.ReadAllBytesAsync(absolutePath);
                base64Images.Add(Convert.ToBase64String(bytes));
                readSuccessCount++;
            }

            if (base64Images.Count == 0)
            {
                continue;
            }

            var request = new UrbanAttachmentUploadRequestDto
            {
                BuildLicenseNo = licenseNo,
                AttachType = group.Key,
                Images = base64Images.ToArray()
            };

            var response = await _urbanManagementApi.UploadAttachmentsAsync(request);
            if (response.AttachmentIds is { Count: > 0 })
            {
                serverIds.AddRange(response.AttachmentIds);
            }
        }

        if (expectedCount > 0 && readSuccessCount == 0)
        {
            throw new InvalidOperationException(
                $"All local attachment files missing for weighing record {weighingRecordId}");
        }

        return serverIds;
    }
}
