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
using Volo.Abp.Domain.Repositories;

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

    /// <summary>
    ///     Upload capture files linked on a passage row by local attachment id.
    /// </summary>
    Task<IReadOnlyList<Guid>> UploadPassageAttachmentsAsync(
        int? largeImageAttachmentId,
        int? smallImageAttachmentId,
        string buildLicenseNo);
}

/// <inheritdoc />
[AutoConstructor]
public partial class UrbanAttachmentSyncService : IUrbanAttachmentSyncService
{
    private const string UnknownAccessCode = "unknown";

    private static readonly AttachType[] UrbanSyncAttachTypes = [AttachType.Lpr, AttachType.UrbanPhoto];

    private readonly IAttachmentService _attachmentService;
    private readonly IRepository<AttachmentFile, int> _attachmentFileRepository;
    private readonly IUrbanManagementApi _urbanManagementApi;
    private readonly IUrbanTusAttachmentClient _tusAttachmentClient;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<UrbanAttachmentSyncService> _logger;

    public async Task<IReadOnlyList<Guid>> UploadAttachmentsAsync(long weighingRecordId, string buildLicenseNo)
    {
        var settings = await _settingsService.GetSettingsAsync();
        if (settings.SystemSettings.EnableChunkedAttachmentUpload)
        {
            return await UploadAttachmentsTusAsync(weighingRecordId, buildLicenseNo);
        }

        return await UploadAttachmentsMultipartInternalAsync(weighingRecordId, buildLicenseNo);
    }

    public async Task<IReadOnlyList<Guid>> UploadPassageAttachmentsAsync(
        int? largeImageAttachmentId,
        int? smallImageAttachmentId,
        string buildLicenseNo)
    {
        var settings = await _settingsService.GetSettingsAsync();
        if (settings.SystemSettings.EnableChunkedAttachmentUpload)
        {
            _logger.LogWarning(
                "Passage attachment upload via tus is not implemented; falling back to multipart for passage attachments");
        }

        var licenseNo = string.IsNullOrWhiteSpace(buildLicenseNo) ? UnknownAccessCode : buildLicenseNo.Trim();
        var attachmentIds = new[] { largeImageAttachmentId, smallImageAttachmentId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (attachmentIds.Count == 0)
        {
            return [];
        }

        var files = await _attachmentFileRepository.GetListAsync(f => attachmentIds.Contains(f.Id));
        if (files.Count == 0)
        {
            return [];
        }

        var serverIds = new List<Guid>();
        foreach (var group in files.GroupBy(f => f.AttachType))
        {
            var filePaths = new List<string>();
            foreach (var attachment in group)
            {
                var absolutePath = PathManager.ToAbsolutePath(attachment.LocalPath);
                if (!File.Exists(absolutePath))
                {
                    _logger.LogWarning(
                        "Passage attachment file missing, FileId={FileId}, Path={Path}",
                        attachment.Id,
                        attachment.LocalPath);
                    continue;
                }

                filePaths.Add(absolutePath);
            }

            if (filePaths.Count == 0)
            {
                continue;
            }

            var streams = new List<FileStream>();
            try
            {
                var parts = new List<StreamPart>(filePaths.Count);
                foreach (var path in filePaths)
                {
                    var stream = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 4096,
                        options: FileOptions.Asynchronous | FileOptions.SequentialScan);
                    streams.Add(stream);
                    parts.Add(new StreamPart(stream, Path.GetFileName(path), "image/jpeg", "files"));
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

        return serverIds;
    }

    private async Task<IReadOnlyList<Guid>> UploadAttachmentsMultipartInternalAsync(
        long weighingRecordId,
        string buildLicenseNo)
    {
        var licenseNo = string.IsNullOrWhiteSpace(buildLicenseNo) ? UnknownAccessCode : buildLicenseNo.Trim();
        if (licenseNo == UnknownAccessCode)
        {
            _logger.LogWarning(
                "AccessCode missing for record {RecordId}; using placeholder for attachment upload path",
                weighingRecordId);
        }

        var prepared = await PrepareUrbanAttachmentFilesAsync(weighingRecordId);
        if (prepared.Groups.Count == 0)
        {
            if (prepared.ExpectedCount > 0 && prepared.ReadSuccessCount == 0)
            {
                throw new InvalidOperationException(
                    $"All local attachment files missing for weighing record {weighingRecordId}");
            }

            return [];
        }

        var serverIds = new List<Guid>();
        foreach (var group in prepared.Groups)
        {
            _logger.LogInformation(
                "Uploading attachments (multipart) for record {RecordId}: AttachType={AttachType}, ImageCount={ImageCount}, FileSizesBytes=[{FileSizesBytes}], TotalFileBytes={TotalFileBytes}",
                weighingRecordId,
                group.AttachType,
                group.FilePaths.Count,
                string.Join(", ", group.FileSizesBytes),
                group.FileSizesBytes.Sum());

            var streams = new List<FileStream>();
            try
            {
                var parts = new List<StreamPart>(group.FilePaths.Count);
                for (var i = 0; i < group.FilePaths.Count; i++)
                {
                    var stream = new FileStream(
                        group.FilePaths[i],
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 4096,
                        options: FileOptions.Asynchronous | FileOptions.SequentialScan);
                    streams.Add(stream);
                    parts.Add(new StreamPart(stream, Path.GetFileName(group.FilePaths[i]), "image/jpeg", "files"));
                }

                var response = await _urbanManagementApi.UploadAttachmentsMultipartAsync(
                    licenseNo,
                    (short)group.AttachType,
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

        return serverIds;
    }

    private async Task<IReadOnlyList<Guid>> UploadAttachmentsTusAsync(long weighingRecordId, string buildLicenseNo)
    {
        var licenseNo = string.IsNullOrWhiteSpace(buildLicenseNo) ? UnknownAccessCode : buildLicenseNo.Trim();
        if (licenseNo == UnknownAccessCode)
        {
            _logger.LogWarning(
                "AccessCode missing for record {RecordId}; using placeholder for tus attachment upload path",
                weighingRecordId);
        }

        var prepared = await PrepareUrbanAttachmentFilesAsync(weighingRecordId);
        if (prepared.Groups.Count == 0)
        {
            if (prepared.ExpectedCount > 0 && prepared.ReadSuccessCount == 0)
            {
                throw new InvalidOperationException(
                    $"All local attachment files missing for weighing record {weighingRecordId}");
            }

            return [];
        }

        var serverIds = new List<Guid>();
        foreach (var group in prepared.Groups)
        {
            _logger.LogInformation(
                "Uploading attachments (tus) for record {RecordId}: AttachType={AttachType}, ImageCount={ImageCount}, FileSizesBytes=[{FileSizesBytes}], TotalFileBytes={TotalFileBytes}",
                weighingRecordId,
                group.AttachType,
                group.FilePaths.Count,
                string.Join(", ", group.FileSizesBytes),
                group.FileSizesBytes.Sum());

            var tusFileIds = new List<string>(group.FilePaths.Count);
            foreach (var path in group.FilePaths)
            {
                var tusFileId = await _tusAttachmentClient.UploadFileAsync(
                    path,
                    licenseNo,
                    group.AttachType);
                tusFileIds.Add(tusFileId);
            }

            // Brief wait for OnFileComplete to persist mapping if it races with commit.
            Exception? lastCommitError = null;
            var committed = false;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    var response = await _urbanManagementApi.CommitTusAttachmentsAsync(
                        new TusAttachmentCommitRequestDto { FileIds = tusFileIds });
                    if (response.AttachmentIds is { Count: > 0 })
                    {
                        serverIds.AddRange(response.AttachmentIds);
                    }

                    committed = true;
                    break;
                }
                catch (Exception ex) when (ex is ApiException or HttpRequestException)
                {
                    lastCommitError = ex;
                    await Task.Delay(200 * (attempt + 1));
                }
            }

            if (!committed)
            {
                throw new InvalidOperationException(
                    $"Tus commit failed for record {weighingRecordId} after retries",
                    lastCommitError);
            }
        }

        return serverIds;
    }

    private async Task<PreparedUrbanAttachments> PrepareUrbanAttachmentFilesAsync(long weighingRecordId)
    {
        var attachmentsByRecord =
            await _attachmentService.GetAttachmentsByWeighingRecordIdsAsync([weighingRecordId]);

        if (!attachmentsByRecord.TryGetValue(weighingRecordId, out var attachments) || attachments.Count == 0)
        {
            return new PreparedUrbanAttachments(0, 0, []);
        }

        var urbanAttachments = attachments
            .Where(a => UrbanSyncAttachTypes.Contains(a.AttachType))
            .ToList();

        if (urbanAttachments.Count == 0)
        {
            return new PreparedUrbanAttachments(0, 0, []);
        }

        var expectedCount = 0;
        var readSuccessCount = 0;
        var groups = new List<PreparedAttachmentGroup>();

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

            if (filePaths.Count > 0)
            {
                groups.Add(new PreparedAttachmentGroup(group.Key, filePaths, fileSizesBytes));
            }
        }

        return new PreparedUrbanAttachments(expectedCount, readSuccessCount, groups);
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

    private sealed record PreparedAttachmentGroup(
        AttachType AttachType,
        List<string> FilePaths,
        List<long> FileSizesBytes);

    private sealed record PreparedUrbanAttachments(
        int ExpectedCount,
        int ReadSuccessCount,
        List<PreparedAttachmentGroup> Groups);
}
