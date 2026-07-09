using MaterialClient.Recycle.Models;
using Refit;

namespace MaterialClient.Recycle.Api;

/// <summary>
///     资源化利用厂数据接入接口（§2.2 端点）Refit 客户端。
///     HMAC-SHA256 签名由 <c>RecycleHmacDelegatingHandler</c> 在 HttpClient 管道中自动注入，
///     故接口方法签名无需携带签名参数。
/// </summary>
[Headers("Content-Type: application/json")]
public interface IRecycleDataApi
{
    /// <summary>
    ///     批量新增出场运输记录。请求体为 JSON Array。
    /// </summary>
    /// <param name="records">运输记录列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>统一响应（code==200 表示成功）</returns>
    [Post("/dataCenter/resourcePlace/productTransportRecord/v1/addBatch")]
    Task<RecycleApiResponse> SubmitTransportRecordAsync(
        [Body] List<RecycleTransportRecord> records,
        CancellationToken cancellationToken = default);
}
