using System.IO;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Utils;

/// <summary>
/// 附件路径工具类（静态方法）
/// </summary>
public static class AttachmentPathUtils
{
    /// <summary>
    /// 根据附件类型获取基础路径
    /// </summary>
    /// <param name="attachType">附件类型</param>
    /// <returns>基础路径：PhotoPiaoJu 或 PhotoJianKong</returns>
    public static string GetBasePath(AttachType attachType)
    {
        return attachType == AttachType.TicketPhoto ? "PhotoPiaoJu" : "PhotoJianKong";
    }

    /// <summary>
    /// 获取附件存储路径（包含日期目录，使用正斜杠，适用于OSS）
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
    /// 获取本地存储路径（包含日期目录，使用反斜杠，适用于Windows文件系统）
    /// </summary>
    /// <param name="attachType">附件类型</param>
    /// <param name="date">日期，如果为 null 则使用当前日期</param>
    /// <returns>路径格式：{basePath}\{year}\{MM}\{dd}\</returns>
    public static string GetLocalStoragePath(AttachType attachType, DateTime? date = null)
    {
        var now = date ?? DateTime.Now;
        var basePath = GetBasePath(attachType);
        return $"{basePath}\\{now.Year}\\{now:MM}\\{now:dd}\\";
    }

    /// <summary>
    /// 生成票据照片文件名
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
    /// 生成监控照片文件名
    /// </summary>
    /// <param name="cameraName">摄像头名称</param>
    /// <param name="channel">通道号</param>
    /// <returns>文件名格式：{cameraName}_{channel}_{guid}.jpg</returns>
    public static string GenerateMonitoringPhotoFileName(string cameraName, int channel)
    {
        return $"{cameraName}_{channel}_{Guid.NewGuid():N}.jpg";
    }

    /// <summary>
    /// 获取完整的本地文件路径（票据照片）
    /// </summary>
    /// <param name="attachType">附件类型</param>
    /// <param name="date">日期，如果为 null 则使用当前日期</param>
    /// <returns>完整路径：{basePath}\{year}\{MM}\{dd}\bill_{timestamp}.jpg</returns>
    public static string GetBillPhotoFullPath(AttachType attachType, DateTime? date = null)
    {
        var basePath = GetLocalStoragePath(attachType, date);
        var fileName = GenerateBillPhotoFileName(date);
        return Path.Combine(basePath, fileName);
    }

    /// <summary>
    /// 获取完整的本地文件路径（监控照片）
    /// </summary>
    /// <param name="attachType">附件类型</param>
    /// <param name="cameraName">摄像头名称</param>
    /// <param name="channel">通道号</param>
    /// <param name="date">日期，如果为 null 则使用当前日期</param>
    /// <returns>完整路径：{basePath}\{year}\{MM}\{dd}\{cameraName}_{channel}_{guid}.jpg</returns>
    public static string GetMonitoringPhotoFullPath(AttachType attachType, string cameraName, int channel, DateTime? date = null)
    {
        var basePath = GetLocalStoragePath(attachType, date);
        var fileName = GenerateMonitoringPhotoFileName(cameraName, channel);
        return Path.Combine(basePath, fileName);
    }

    /// <summary>
    /// 获取完整的OSS对象键（包含文件名）
    /// </summary>
    /// <param name="attachType">附件类型</param>
    /// <param name="attachmentId">附件ID</param>
    /// <param name="fileName">文件名</param>
    /// <param name="date">日期，如果为 null 则使用当前日期</param>
    /// <returns>完整路径：{basePath}/{year}/{MM}/{dd}/{attachmentId}_{fileName}</returns>
    public static string GetOssObjectKey(AttachType attachType, int attachmentId, string fileName, DateTime? date = null)
    {
        var path = GetStoragePath(attachType, date);
        return $"{path}{attachmentId}_{fileName}";
    }
}

