using MaterialClient.Urban.Dtos;
using Refit;

namespace MaterialClient.Urban.Api;

/// <summary>
///     UrbanManagement 服务端 Refit API 接口
/// </summary>
[Headers("Content-Type: application/json")]
public interface IUrbanManagementApi
{
    /// <summary>
    ///     提交称重记录到 UrbanManagement 服务端
    /// </summary>
    [Post("/api/urban/weighing-records")]
    Task<UrbanApiResponse<UrbanWeighingRecordResult>> SubmitWeighingRecordAsync(
        [Body] UrbanWeighingRecordSubmitDto dto);
}

/// <summary>
///     UrbanManagement API 响应包装
/// </summary>
public class UrbanApiResponse<T>
{
    public bool Success { get; set; }
    public string? Msg { get; set; }
    public T? Data { get; set; }
}

/// <summary>
///     称重记录提交结果
/// </summary>
public class UrbanWeighingRecordResult
{
    public long Id { get; set; }
}
