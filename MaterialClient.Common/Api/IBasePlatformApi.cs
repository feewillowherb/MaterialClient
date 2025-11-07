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
    Task<HttpResult<LicenseInfoDto>> GetAuthClientLicenseAsync(
        [Body] LicenseRequestDto request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="request">登录请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>登录用户信息</returns>
    [Post("/User/UserLogin")]
    Task<HttpResult<LoginUserDto>> UserLoginAsync(
        [Body] LoginRequestDto request,
        CancellationToken cancellationToken = default);
}

