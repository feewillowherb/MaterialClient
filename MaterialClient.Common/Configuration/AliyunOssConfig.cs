namespace MaterialClient.Common.Configuration;

/// <summary>
///     阿里云OSS配置
/// </summary>
public class AliyunOssConfig
{
    /// <summary>
    ///     OSS区域ID
    /// </summary>
    public string RegionId { get; set; } = "oss-cn-hangzhou.aliyuncs.com";

    /// <summary>
    ///     OSS访问密钥ID
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    ///     OSS访问密钥Secret
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    ///     OSS存储桶名称
    /// </summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    ///     OSS最大文件大小（MB）
    /// </summary>
    public string MaxSize { get; set; } = "100";
}