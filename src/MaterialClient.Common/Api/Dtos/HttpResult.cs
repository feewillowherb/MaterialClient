namespace MaterialClient.Common.Api.Dtos;

/// <summary>
///     基础平台API统一响应包装类
/// </summary>
/// <typeparam name="T">响应数据类型</typeparam>
public class HttpResult<T>
{
    /// <summary>
    ///     状态码（0表示成功）
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    ///     响应数据
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    ///     响应消息
    /// </summary>
    public string? Msg { get; set; }

    /// <summary>
    ///     是否成功
    /// </summary>
    public bool Success { get; set; }
}