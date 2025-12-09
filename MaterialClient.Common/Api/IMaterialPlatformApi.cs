using System.Collections.Generic;
using MaterialClient.Common.Api.Dtos;
using Refit;

namespace MaterialClient.Common.Api;

public interface IMaterialPlatformApi
{
    [Post("/api/Material/MaterialGoodList")]
    Task<HttpResult<List<MaterialGoodListResultDto>>> GetMaterialGoodListAsync(
        [Body] GetMaterialGoodListInput request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="request">登录请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>登录用户信息</returns>
    [Post("/api/User/UserLogin")]
    Task<HttpResult<object>> UserLoginAsync(
        [Body] LoginRequestDto request,
        CancellationToken cancellationToken = default);
}

public record GetMaterialGoodListInput(
    string ProId,
    long UpdateTime
);