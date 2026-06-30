using MaterialClient.Common.Api.Dtos;
using Refit;

namespace MaterialClient.Common.Api;

/// <summary>
///     UrbanManagement 授权代理 API（activate）
/// </summary>
[Headers("Content-Type: application/json")]
public interface IUrbanAuthApi
{
    [Post("/api/urban/auth/activate")]
    Task<HttpResult<ActivateUrbanResponseData>> ActivateUrbanAsync(
        [Body] ActivateUrbanRequest request,
        CancellationToken cancellationToken = default);
}
