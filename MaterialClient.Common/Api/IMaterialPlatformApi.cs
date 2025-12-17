using System.Collections.Generic;
using System.ComponentModel;
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

    /// <summary>
    /// 授权机器码
    /// </summary>
    /// <param name="request">授权机器码请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>登录用户信息</returns>
    [Post("/api/ProjectMachine/AuthMachineCode")]
    Task<HttpResult<bool>> AuthMachineCodeAsync(
        [Body] GetAuthMachineCodeInput request,
        CancellationToken cancellationToken = default);


    /// <summary>
    /// 验证项目/机器码
    /// </summary>
    /// <param name="request">验证项目/机器码请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>登录用户信息</returns>
    [Post("/api/ProjectMachine/AuthMachineCode")]
    Task<HttpResult<bool>> VerificationMachineCodeAsync(
        string proId,
        string machineCode,
        //OUT
        VerificationMachineCodeEnum verificationMachineCode,
        CancellationToken cancellationToken = default);
}

public record GetAuthMachineCodeInput(
    string ProId,
    string MachineCode,
    string AuthToken
);

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

public enum VerificationMachineCodeEnum
{
    /// <summary>
    /// 其他异常
    /// </summary>
    [Description("其他")] Other = -1,

    /// <summary>
    /// 未绑定(项目和机器码没有绑定)
    /// </summary>
    [Description("未绑定")] UnBounded = 0,

    /// <summary>
    /// 已绑定(项目和当前机器码一致，等同于相同电脑重新安装软件)
    /// </summary>
    [Description("项目和当前机器码一致")] BoundedWithThisErrorCode = 1,

    /// <summary>
    /// 已绑定(项目相同，机器码不一致)
    /// </summary>
    [Description("项目相同，机器码不一致")] BoundedWithErrorCode = 2,

    /// <summary>
    /// 已绑定（项目不同，机器码一致）
    /// </summary>
    [Description("项目不同，机器码一致")] BoundedWithManyErrorCode = 3,

    /// <summary>
    /// 授权Token验证失败
    /// </summary>
    [Description("授权Token验证失败")] InvalidAuthTokenErrorCode = 4,

    /// <summary>
    /// 授权有效期到期
    /// </summary>
    [Description("授权有效期到期")] AuthEndDateErrorCode = 5,

    /// <summary>
    /// 授权状态
    /// </summary>
    [Description("授权状态无效")] AuthStatusErrorCode = 6,
}