using MaterialClient.Common.Api.Dtos;
using Refit;

namespace MaterialClient.Common.Api;

/// <summary>
/// 基础平台API客户端接口
/// </summary>
public interface IBasePlatformApi
{
    /// <summary>
    /// 获取授权客户端许可证
    /// </summary>
    /// <param name="request">授权请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>授权信息</returns>
    [Post("/api/AuthClientLicense/GetAuthClientLicense")]
    Task<HttpResult<string>> GetAuthClientLicenseAsync(
        [Body] LicenseRequestDto request,
        CancellationToken cancellationToken = default);
    
}