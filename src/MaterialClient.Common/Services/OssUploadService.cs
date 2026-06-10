using Aliyun.OSS;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Models;
using MaterialClient.Common.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     OSS上传服务接口
/// </summary>
public interface IOssUploadService
{
    /// <summary>
    ///     校验OSS配置并确保客户端可用
    /// </summary>
    /// <returns>可用的 OSS 客户端</returns>
    OssClient EnsureOssClient();

    /// <summary>
    ///     上传单个文件到OSS
    /// </summary>
    /// <param name="localPath">本地文件路径</param>
    /// <param name="ossObjectKey">OSS对象键（完整路径）</param>
    /// <returns>OSS完整URL，失败返回null</returns>
    Task<string?> UploadFileAsync(string localPath, string ossObjectKey);

    /// <summary>
    ///     批量上传文件到OSS
    /// </summary>
    /// <param name="attachments">附件文件列表（需要包含waybillId信息）</param>
    /// <returns>上传结果字典，key为AttachmentFile.Id，value为OSS完整路径</returns>
    Task<Dictionary<int, string>> UploadFilesAsync(List<AttachmentWithWaybill> attachments);
}

/// <summary>
///     OSS上传服务实现
/// </summary>
public class OssUploadService : IOssUploadService, ITransientDependency
{
    private readonly IOptions<AliyunOssConfig> _options;
    private readonly ILogger<OssUploadService>? _logger;
    private OssClient? _ossClient;

    public OssUploadService(IOptions<AliyunOssConfig> options, ILogger<OssUploadService>? logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public OssClient EnsureOssClient()
    {
        var config = _options.Value;
        if (!config.IsValid())
        {
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(config.RegionId)) missingFields.Add(nameof(config.RegionId));
            if (string.IsNullOrWhiteSpace(config.Key)) missingFields.Add(nameof(config.Key));
            if (string.IsNullOrWhiteSpace(config.Secret)) missingFields.Add(nameof(config.Secret));
            if (string.IsNullOrWhiteSpace(config.BucketName)) missingFields.Add(nameof(config.BucketName));

            var missing = missingFields.Count > 0 ? string.Join(", ", missingFields) : "Unknown";
            throw new BusinessException("OSS:INVALID_CONFIG", $"OSS配置无效，缺少字段: {missing}");
        }

        _ossClient ??= new OssClient(
            config.RegionId,
            config.Key,
            config.Secret);

        return _ossClient;
    }

    /// <inheritdoc />
    public async Task<string?> UploadFileAsync(string localPath, string ossObjectKey)
    {
        var ossClient = EnsureOssClient();
        var config = _options.Value;
        var bucketName = config.BucketName;

        try
        {
            // Normalize before File API usage (localPath may be stored as relative path in DB).
            localPath = AttachmentPathUtils.ToAbsolutePath(localPath);
            if (!File.Exists(localPath))
            {
                _logger?.LogWarning("本地文件不存在: {LocalPath}", localPath);
                return null;
            }

            await Task.Run(() => { ossClient.PutObject(bucketName, ossObjectKey, localPath); });

            // 构建OSS完整URL
            var ossUrl = config.GetOssUrl(ossObjectKey);
            _logger?.LogInformation("文件上传成功: {LocalPath} -> {OssUrl}", localPath, ossUrl);
            return ossUrl;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "文件上传失败: {LocalPath}, OSS Key: {OssObjectKey}", localPath,
                ossObjectKey);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<int, string>> UploadFilesAsync(List<AttachmentWithWaybill> attachments)
    {
        var result = new Dictionary<int, string>();

        if (attachments == null || attachments.Count == 0)
            return result;

        var ossClient = EnsureOssClient();
        var config = _options.Value;
        var bucketName = config.BucketName;

        foreach (var item in attachments)
            try
            {
                if (string.IsNullOrWhiteSpace(item.Attachment.LocalPath))
                {
                    _logger?.LogWarning(
                        " 跳过不存在的文件: AttachmentId={AttachmentId}, LocalPath={LocalPath}",
                        item.Attachment.Id, item.Attachment.LocalPath);
                    continue;
                }

                var normalizedLocalPath = AttachmentPathUtils.ToAbsolutePath(item.Attachment.LocalPath);
                if (!File.Exists(normalizedLocalPath))
                {
                    _logger?.LogWarning(
                        " 跳过不存在的文件: AttachmentId={AttachmentId}, LocalPath={LocalPath} (normalized={NormalizedLocalPath})",
                        item.Attachment.Id, item.Attachment.LocalPath, normalizedLocalPath);
                    continue;
                }

                // 根据附件类型构建OSS对象键
                var fileName = Path.GetFileName(normalizedLocalPath);
                var ossObjectKey = AttachmentPathUtils.GetOssObjectKey(
                    item.Attachment.AttachType,
                    item.Attachment.Id,
                    fileName);

                await Task.Run(() =>
                    {
                        ossClient.PutObject(bucketName, ossObjectKey, normalizedLocalPath);
                    });

                // 构建OSS完整URL
                var ossUrl = config.GetOssUrl(ossObjectKey);
                result[item.Attachment.Id] = ossUrl;

                _logger?.LogInformation(
                    " 附件上传成功: AttachmentId={AttachmentId}, WaybillId={WaybillId}, OssUrl={OssUrl}",
                    item.Attachment.Id, item.WaybillId, ossUrl);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    " 附件上传失败: AttachmentId={AttachmentId}, WaybillId={WaybillId}, LocalPath={LocalPath}",
                    item.Attachment.Id, item.WaybillId, item.Attachment.LocalPath);
                // 继续处理下一个文件，不中断批量上传
            }

        return result;
    }
}