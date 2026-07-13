using System.Text.Json.Serialization;

namespace MaterialClient.Recycle.Models;

/// <summary>
///     §2.2 接口统一响应 DTO。
/// </summary>
public class RecycleApiResponse
{
    /// <summary>状态码（200 表示成功）</summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>错误/结果描述</summary>
    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    /// <summary>信息结构（成功时通常为 null 或业务数据）</summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }
}
