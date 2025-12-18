namespace MaterialClient.Common.Api.Dtos;

/// <summary>
/// 登录请求DTO
/// </summary>
public class LoginRequestDto
{
    /// <summary>
    /// 用户名（手机号）
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 用户密码
    /// </summary>
    public string? UserPwd { get; set; }

    /// <summary>
    /// 项目Id
    /// </summary>
    public string? ProId { get; set; }

    /// <summary>
    /// 客户端Id
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
}