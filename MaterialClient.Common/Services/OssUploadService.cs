using System.IO;
using Aliyun.OSS;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
/// OSS上传服务实现
/// </summary>
public class OssUploadService : IOssUploadService, ITransientDependency
{
    private readonly OssClient _ossClient;
    private readonly AliyunOssConfig _config;
    private readonly ILogger<OssUploadService>? _logger;

    public OssUploadService(IOptions<AliyunOssConfig> options, ILogger<OssUploadService>? logger)
    {
        _config = options.Value;
        _logger = logger;
        _ossClient = new OssClient(
            _config.RegionId,
            _config.Key,
            _config.Secret);
    }

    /// <inheritdoc />
    public async Task<string?> UploadFileAsync(string localPath, string ossObjectKey)
    {
        try
        {
            if (!File.Exists(localPath))
            {
                _logger?.LogWarning("OssUploadService: 本地文件不存在: {LocalPath}", localPath);
                return null;
            }

            var bucketName = _config.BucketName;

            await Task.Run(() =>
            {
                _ossClient.PutObject(bucketName, ossObjectKey, localPath);
            });

            // 构建OSS完整URL
            var ossUrl = $"https://{bucketName}.{_config.RegionId}/{ossObjectKey}";
            _logger?.LogInformation("OssUploadService: 文件上传成功: {LocalPath} -> {OssUrl}", localPath, ossUrl);
            return ossUrl;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "OssUploadService: 文件上传失败: {LocalPath}, OSS Key: {OssObjectKey}", localPath, ossObjectKey);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<int, string>> UploadFilesAsync(List<(AttachmentFile attachment, long waybillId)> attachments)
    {
        var result = new Dictionary<int, string>();

        if (attachments == null || attachments.Count == 0)
            return result;

        var bucketName = _config.BucketName;

        foreach (var (attachment, waybillId) in attachments)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(attachment.LocalPath) || !File.Exists(attachment.LocalPath))
                {
                    _logger?.LogWarning("OssUploadService: 跳过不存在的文件: AttachmentId={AttachmentId}, LocalPath={LocalPath}",
                        attachment.Id, attachment.LocalPath);
                    continue;
                }

                // 构建OSS对象键：waybill/{waybillId}/{attachmentId}_{fileName}
                var fileName = Path.GetFileName(attachment.LocalPath);
                var ossObjectKey = $"waybill/{waybillId}/{attachment.Id}_{fileName}";

                await Task.Run(() =>
                {
                    _ossClient.PutObject(bucketName, ossObjectKey, attachment.LocalPath);
                });

                // 构建OSS完整URL
                var ossUrl = $"https://{bucketName}.{_config.RegionId}/{ossObjectKey}";
                result[attachment.Id] = ossUrl;

                _logger?.LogInformation("OssUploadService: 附件上传成功: AttachmentId={AttachmentId}, WaybillId={WaybillId}, OssUrl={OssUrl}",
                    attachment.Id, waybillId, ossUrl);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OssUploadService: 附件上传失败: AttachmentId={AttachmentId}, WaybillId={WaybillId}, LocalPath={LocalPath}",
                    attachment.Id, waybillId, attachment.LocalPath);
                // 继续处理下一个文件，不中断批量上传
            }
        }

        return result;
    }
}

