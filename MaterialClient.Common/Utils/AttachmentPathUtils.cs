using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Utils;

/// <summary>
///     附件路径工具类（静态方法）
/// </summary>
public static class AttachmentPathUtils
{
    /// <summary>
    /// Normalize a local (possibly relative) path to an absolute path based on <see cref="AppContext.BaseDirectory"/>.
    /// This prevents File API calls from depending on the process working directory (e.g., auto-start from System32).
    /// </summary>
    /// <param name="path">Local path (absolute or relative)</param>
    /// <returns>Absolute path for file system operations; empty string for null/empty input.</returns>
    public static string ToAbsolutePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        return PathManager.ToAbsolutePath(path);
    }

    /// <summary>
    /// Check file existence with path normalization.
    /// </summary>
    /// <param name="path">Local path (absolute or relative)</param>
    public static bool FileExists(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var absolutePath = ToAbsolutePath(path);
        return File.Exists(absolutePath);
    }

    /// <summary>
    ///     根据附件类型获取基础路径
    /// </summary>
    /// <param name="attachType">附件类型</param>
    /// <returns>基础路径：PhotoPiaoJu 或 PhotoJianKong</returns>
    public static string GetBasePath(AttachType attachType)
    {
        return attachType == AttachType.TicketPhoto ? "PhotoPiaoJu" : "PhotoJianKong";
    }

    /// <summary>
    ///     获取附件存储路径（包含日期目录，使用正斜杠，适用于OSS）
    /// </summary>
    /// <param name="attachType">附件类型</param>
    /// <param name="date">日期，如果为 null 则使用当前日期</param>
    /// <returns>路径格式：{basePath}/{year}/{MM}/{dd}/</returns>
    public static string GetStoragePath(AttachType attachType, DateTime? date = null)
    {
        var now = date ?? DateTime.Now;
        var basePath = GetBasePath(attachType);
        return $"{basePath}/{now.Year}/{now:MM}/{now:dd}/";
    }

    /// <summary>
    ///     获取本地存储路径（相对路径，包含日期目录）
    /// </summary>
    /// <param name="attachType">附件类型</param>
    /// <param name="date">日期，如果为 null 则使用当前日期</param>
    /// <returns>路径格式：{basePath}\{year}\{MM}\{dd}\</returns>
    public static string GetLocalStoragePath(AttachType attachType, DateTime? date = null)
    {
        var now = date ?? DateTime.Now;
        var basePath = GetBasePath(attachType);
        return $"{basePath}/{now.Year}/{now:MM}/{now:dd}/";
    }

    /// <summary>
    ///     获取本地存储的绝对路径（基于应用程序可执行文件目录）
    ///     用于确保应用从任何工作目录（包括 C:\Windows\System32）启动时都能正确访问附件
    /// </summary>
    /// <param name="attachType">附件类型</param>
    /// <param name="date">日期，如果为 null 则使用当前日期</param>
    /// <returns>绝对路径格式：{AppContext.BaseDirectory}\{basePath}\{year}\{MM}\{dd}\</returns>
    public static string GetLocalStorageAbsolutePath(AttachType attachType, DateTime? date = null)
    {
        var relativePath = GetLocalStoragePath(attachType, date);
        var appDirectory = AppContext.BaseDirectory;
        return Path.Combine(appDirectory, relativePath);
    }

    /// <summary>
    ///     生成票据照片文件名
    /// </summary>
    /// <param name="date">日期，如果为 null 则使用当前日期</param>
    /// <returns>文件名格式：bill_{yyyyMMddHHmmss}.jpg</returns>
    public static string GenerateBillPhotoFileName(DateTime? date = null)
    {
        var now = date ?? DateTime.Now;
        var timestamp = now.ToString("yyyyMMddHHmmss");
        return $"bill_{timestamp}.jpg";
    }

    /// <summary>
    ///     生成监控照片文件名
    /// </summary>
    /// <param name="cameraName">摄像头名称</param>
    /// <param name="channel">通道号</param>
    /// <returns>文件名格式：{cameraName}_{channel}_{guid}.jpg</returns>
    public static string GenerateMonitoringPhotoFileName(string cameraName, int channel)
    {
        return $"{cameraName}_{channel}_{Guid.NewGuid():N}.jpg";
    }

    /// <summary>
    ///     获取完整的本地文件路径（票据照片）
    ///     返回绝对路径，确保从任何工作目录启动都能正确访问文件
    /// </summary>
    /// <param name="attachType">附件类型</param>
    /// <param name="date">日期，如果为 null 则使用当前日期</param>
    /// <returns>完整绝对路径：{AppContext.BaseDirectory}\{basePath}\{year}\{MM}\{dd}\bill_{timestamp}.jpg</returns>
    public static string GetBillPhotoFullPath(AttachType attachType, DateTime? date = null)
    {
        var basePath = GetLocalStorageAbsolutePath(attachType, date);
        var fileName = GenerateBillPhotoFileName(date);
        return Path.Combine(basePath, fileName);
    }

    /// <summary>
    ///     获取完整的本地文件路径（监控照片）
    ///     返回绝对路径，确保从任何工作目录启动都能正确访问文件
    /// </summary>
    /// <param name="attachType">附件类型</param>
    /// <param name="cameraName">摄像头名称</param>
    /// <param name="channel">通道号</param>
    /// <param name="date">日期，如果为 null 则使用当前日期</param>
    /// <returns>完整绝对路径：{AppContext.BaseDirectory}\{basePath}\{year}\{MM}\{dd}\{cameraName}_{channel}_{guid}.jpg</returns>
    public static string GetMonitoringPhotoFullPath(AttachType attachType, string cameraName, int channel,
        DateTime? date = null)
    {
        var basePath = GetLocalStorageAbsolutePath(attachType, date);
        var fileName = GenerateMonitoringPhotoFileName(cameraName, channel);
        return Path.Combine(basePath, fileName);
    }

    /// <summary>
    ///     获取完整的OSS对象键（包含文件名）
    /// </summary>
    /// <param name="attachType">附件类型</param>
    /// <param name="attachmentId">附件ID</param>
    /// <param name="fileName">文件名</param>
    /// <param name="date">日期，如果为 null 则使用当前日期</param>
    /// <returns>完整路径：{basePath}/{year}/{MM}/{dd}/{attachmentId}_{fileName}</returns>
    public static string GetOssObjectKey(AttachType attachType, int attachmentId, string fileName,
        DateTime? date = null)
    {
        var path = GetStoragePath(attachType, date);
        return $"{path}{attachmentId}_{fileName}";
    }
}