namespace MaterialClient.Common.Api.Dtos;

/// <summary>
/// 登录用户信息DTO
/// </summary>
public class LoginUserDto
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 用户名（手机号）
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 客户端标识
    /// </summary>
    public Guid ClientId { get; set; }

    /// <summary>
    /// 真实姓名
    /// </summary>
    public string TrueName { get; set; } = string.Empty;

    /// <summary>
    /// 是否管理员
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// 是否企业
    /// </summary>
    public bool IsCompany { get; set; }

    /// <summary>
    /// 产品分类（1旧版本，2新版本）
    /// </summary>
    public int ProductType { get; set; }

    /// <summary>
    /// 来源产品ID
    /// </summary>
    public long FromProductId { get; set; }

    /// <summary>
    /// 产品ID
    /// </summary>
    public long ProductId { get; set; }

    /// <summary>
    /// 产品名称
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// 公司ID
    /// </summary>
    public int CoId { get; set; }

    /// <summary>
    /// 公司名称
    /// </summary>
    public string CoName { get; set; } = string.Empty;

    /// <summary>
    /// 产品路径
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 登录Token
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 授权到期时间
    /// </summary>
    public DateTime? AuthEndTime { get; set; }
}