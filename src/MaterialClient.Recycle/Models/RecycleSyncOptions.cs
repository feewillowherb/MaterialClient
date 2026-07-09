namespace MaterialClient.Recycle.Models;

/// <summary>
///     Recycle 数据上报同步配置（appsettings.json 中 RecycleSync 配置段）。
/// </summary>
public class RecycleSyncOptions
{
    /// <summary>是否启用上报同步管线。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>§2.2 接口根地址（BaseAddress）。</summary>
    public string ApiUrl { get; set; } = "http://localhost";

    /// <summary>HMAC 签名 accessKey（X-AKZTJG-HMAC-ACCESS-KEY）。待平台方提供。</summary>
    public string? AccessKey { get; set; }

    /// <summary>HMAC 签名 secretKey（HMAC-SHA256 密钥）。待平台方提供。</summary>
    public string? SecretKey { get; set; }

    /// <summary>资源化利用厂唯一标识（pointNumber，待运营方提供）。</summary>
    public string? PointNumber { get; set; }

    /// <summary>轮询扫描间隔（秒），默认 5。</summary>
    public int PollIntervalSeconds { get; set; } = 5;

    /// <summary>单条记录最大重试次数，达到后放弃（SyncStatus=Failed），默认 9。</summary>
    public int MaxFailCount { get; set; } = 9;

    /// <summary>HTTP 请求超时（秒），默认 30。</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
