using System.Text.Json.Serialization;
using MaterialClient.Urban.Dtos;
using Refit;

namespace MaterialClient.Urban.Api;

/// <summary>
///     UrbanManagement 服务端 Refit API 接口（ABP 约定路由）
/// </summary>
[Headers("Content-Type: application/json")]
public interface IUrbanManagementApi
{
    /// <summary>
    ///     接收称重记录（对应 <see cref="UrbanManagement.Core.Services.IUrbanWeighingRecordAppService.ReceiveAsync"/>）
    /// </summary>
    [Post("/api/app/urban-weighing-record/receive")]
    Task<UrbanWeighingRecordReceiveResult> ReceiveWeighingRecordAsync(
        [Body] UrbanWeighingRecordSubmitDto dto);
}

/// <summary>
///     称重记录接收结果（与 UrbanManagement UrbanWeighingRecordReceiveOutputDto 对齐）
/// </summary>
public class UrbanWeighingRecordReceiveResult
{
    [JsonPropertyName("recordId")]
    public long RecordId { get; set; }
}
