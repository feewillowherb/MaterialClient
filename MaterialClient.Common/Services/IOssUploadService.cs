using MaterialClient.Common.Entities;

namespace MaterialClient.Common.Services;

/// <summary>
/// OSS上传服务接口
/// </summary>
public interface IOssUploadService
{
    /// <summary>
    /// 上传单个文件到OSS
    /// </summary>
    /// <param name="localPath">本地文件路径</param>
    /// <param name="ossObjectKey">OSS对象键（完整路径）</param>
    /// <returns>OSS完整URL，失败返回null</returns>
    Task<string?> UploadFileAsync(string localPath, string ossObjectKey);

    /// <summary>
    /// 批量上传文件到OSS
    /// </summary>
    /// <param name="attachments">附件文件列表（需要包含waybillId信息）</param>
    /// <returns>上传结果字典，key为AttachmentFile.Id，value为OSS完整路径</returns>
    Task<Dictionary<int, string>> UploadFilesAsync(List<(AttachmentFile attachment, long waybillId)> attachments);
}

