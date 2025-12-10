using System.Collections.Generic;
using MaterialClient.Common.Api.Dtos;
using Refit;

namespace MaterialClient.Common.Api;

public interface IMaterialPlatformApi
{
    [Post("/api/Material/MaterialGoodList")]
    Task<List<MaterialGoodListResultDto>> GetMaterialGoodListAsync(
        [Body] GetMaterialGoodListInput request,
        CancellationToken cancellationToken = default);


    [Post("/api/Material/MaterialGoodTypeList")]
    Task<List<MaterialGoodTypeListResultDto>> MaterialGoodTypeListAsync(
        [Body] GetMaterialGoodTypeListInput request,
        CancellationToken cancellationToken = default
    );

    [Post("/api/Provider/MaterialProviderList")]
    Task<List<MaterialProviderListResultDto>> MaterialProviderListAsync(
        [Body] GetMaterialProviderListInput request,
        CancellationToken cancellationToken = default
    );


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

public record GetMaterialGoodTypeListInput(
    string ProId,
    long UpdateTime
);

public record GetMaterialProviderListInput(
    string ProId,
    long UploadTime
);