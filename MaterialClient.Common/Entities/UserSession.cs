using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 用户会话实体
/// 存储用户的登录会话信息
/// </summary>
[Table("UserSessions")]
public class UserSession : Entity<Guid>
{
    /// <summary>
    /// 构造函数（用于EF Core）
    /// </summary>
    private UserSession()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public UserSession(
        Guid id,
        Guid projectId,
        Guid licenseInfoId,
        long userId,
        string username,
        string trueName,
        Guid clientId,
        string accessToken,
        bool isAdmin,
        bool isCompany,
        int productType,
        long fromProductId,
        long productId,
        string productName,
        int companyId,
        string companyName,
        string apiUrl,
        DateTime? authEndTime)
        : base(id)
    {
        ProjectId = projectId;
        UserId = userId;
        Username = username;
        TrueName = trueName;
        ClientId = clientId;
        AccessToken = accessToken;
        IsAdmin = isAdmin;
        IsCompany = isCompany;
        ProductType = productType;
        FromProductId = fromProductId;
        ProductId = productId;
        ProductName = productName;
        CompanyId = companyId;
        CompanyName = companyName;
        ApiUrl = apiUrl;
        AuthEndTime = authEndTime;
        LicenseInfoId = licenseInfoId;
        LoginTime = DateTime.Now;
        LastActivityTime = DateTime.Now;
    }

    /// <summary>
    /// 项目ID
    /// </summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// 授权信息ID
    /// </summary>
    [Required]
    public Guid LicenseInfoId { get; set; }

    /// <summary>
    /// 用户ID（从基础平台获取）
    /// </summary>
    [Required]
    public long UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Username { get; set; }

    /// <summary>
    /// 真实姓名
    /// </summary>
    [MaxLength(100)]
    public string TrueName { get; set; }

    /// <summary>
    /// 客户端ID（从基础平台获取）
    /// </summary>
    [Required]
    public Guid ClientId { get; set; }

    /// <summary>
    /// 访问令牌（从基础平台获取）
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string AccessToken { get; set; }

    /// <summary>
    /// 是否是管理员
    /// </summary>
    [Required]
    public bool IsAdmin { get; set; }

    /// <summary>
    /// 是否是公司账号
    /// </summary>
    [Required]
    public bool IsCompany { get; set; }

    /// <summary>
    /// 产品类型
    /// </summary>
    [Required]
    public int ProductType { get; set; }

    /// <summary>
    /// 来源产品ID
    /// </summary>
    [Required]
    public long FromProductId { get; set; }

    /// <summary>
    /// 产品ID
    /// </summary>
    [Required]
    public long ProductId { get; set; }

    /// <summary>
    /// 产品名称
    /// </summary>
    [MaxLength(200)]
    public string ProductName { get; set; }

    /// <summary>
    /// 公司ID
    /// </summary>
    [Required]
    public int CompanyId { get; set; }

    /// <summary>
    /// 公司名称
    /// </summary>
    [MaxLength(200)]
    public string CompanyName { get; set; }

    /// <summary>
    /// API URL
    /// </summary>
    [MaxLength(500)]
    public string ApiUrl { get; set; }

    /// <summary>
    /// 授权结束时间（来自基础平台）
    /// </summary>
    public DateTime? AuthEndTime { get; set; }

    /// <summary>
    /// 登录时间
    /// </summary>
    [Required]
    public DateTime LoginTime { get; set; }

    /// <summary>
    /// 最后活动时间
    /// </summary>
    [Required]
    public DateTime LastActivityTime { get; set; }

    /// <summary>
    /// 会话是否过期（超过24小时无活动）
    /// </summary>
    public bool IsExpired => (DateTime.Now - LastActivityTime).TotalHours > 24;

    /// <summary>
    /// 更新最后活动时间
    /// </summary>
    public void UpdateActivity()
    {
        LastActivityTime = DateTime.Now;
    }

    /// <summary>
    /// 更新访问令牌
    /// </summary>
    public void UpdateAccessToken(string newToken)
    {
        AccessToken = newToken;
        LastActivityTime = DateTime.Now;
    }
}

