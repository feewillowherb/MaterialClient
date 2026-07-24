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
using Refit;
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
    private const string UnknownAccessCode = "unknown";

    private static readonly AttachType[] UrbanSyncAttachTypes = [AttachType.Lpr, AttachType.UrbanPhoto];

    private readonly IAttachmentService _attachmentService;
    private readonly IUrbanManagementApi _urbanManagementApi;
    private readonly ILogger<UrbanAttachmentSyncService> _logger;

    public async Task<IReadOnlyList<Guid>> UploadAttachmentsAsync(long weighingRecordId, string buildLicenseNo)
    {
        var licenseNo = string.IsNullOrWhiteSpace(buildLicenseNo) ? UnknownAccessCode : buildLicenseNo.Trim();
        if (licenseNo == UnknownAccessCode)
        {
            _logger.LogWarning(
                "AccessCode missing for record {RecordId}; using placeholder for attachment upload path",
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
            var filePaths = new List<string>();
            var fileSizesBytes = new List<long>();
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

                var fileInfo = new FileInfo(absolutePath);
                fileSizesBytes.Add(fileInfo.Length);
                filePaths.Add(absolutePath);
                readSuccessCount++;
            }

            if (filePaths.Count == 0)
            {
                continue;
            }

            var totalFileBytes = fileSizesBytes.Sum();
            _logger.LogInformation(
                "Uploading attachments (multipart) for record {RecordId}: AttachType={AttachType}, ImageCount={ImageCount}, FileSizesBytes=[{FileSizesBytes}], TotalFileBytes={TotalFileBytes}",
                weighingRecordId,
                group.Key,
                filePaths.Count,
                string.Join(", ", fileSizesBytes),
                totalFileBytes);

            var streams = new List<FileStream>();
            try
            {
                var parts = new List<StreamPart>(filePaths.Count);
                for (var i = 0; i < filePaths.Count; i++)
                {
                    var stream = new FileStream(
                        filePaths[i],
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 4096,
                        options: FileOptions.Asynchronous | FileOptions.SequentialScan);
                    streams.Add(stream);
                    parts.Add(new StreamPart(stream, Path.GetFileName(filePaths[i]), "image/jpeg", "files"));
                }

                var response = await _urbanManagementApi.UploadAttachmentsMultipartAsync(
                    licenseNo,
                    (short)group.Key,
                    parts);

                if (response.AttachmentIds is { Count: > 0 })
                {
                    serverIds.AddRange(response.AttachmentIds);
                }
            }
            finally
            {
                foreach (var stream in streams)
                {
                    await stream.DisposeAsync();
                }
            }
        }

        if (expectedCount > 0 && readSuccessCount == 0)
        {
            throw new InvalidOperationException(
                $"All local attachment files missing for weighing record {weighingRecordId}");
        }

        return serverIds;
    }

    /// <summary>
    ///     Legacy Base64 JSON upload path. Retained for rollback; default sync uses multipart.
    ///     Do not delete until all clients have migrated and a remove-* change retires the server API.
    /// </summary>
    internal async Task<IReadOnlyList<Guid>> UploadAttachmentsBase64Async(long weighingRecordId, string buildLicenseNo)
    {
        var licenseNo = string.IsNullOrWhiteSpace(buildLicenseNo) ? UnknownAccessCode : buildLicenseNo.Trim();
        if (licenseNo == UnknownAccessCode)
        {
            _logger.LogWarning(
                "AccessCode missing for record {RecordId}; using placeholder for attachment upload path",
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
            var fileSizesBytes = new List<long>();
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
                fileSizesBytes.Add(bytes.LongLength);
                base64Images.Add(Convert.ToBase64String(bytes));
                readSuccessCount++;
            }

            if (base64Images.Count == 0)
            {
                continue;
            }

            var totalFileBytes = fileSizesBytes.Sum();
            var totalBase64Chars = base64Images.Sum(s => (long)s.Length);
            _logger.LogInformation(
                "Uploading attachments (legacy Base64) for record {RecordId}: AttachType={AttachType}, ImageCount={ImageCount}, FileSizesBytes=[{FileSizesBytes}], TotalFileBytes={TotalFileBytes}, TotalBase64Chars={TotalBase64Chars}",
                weighingRecordId,
                group.Key,
                base64Images.Count,
                string.Join(", ", fileSizesBytes),
                totalFileBytes,
                totalBase64Chars);

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
