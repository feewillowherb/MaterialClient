using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 用户凭证实体
/// 存储用户的本地登录凭证（用于"记住密码"功能）
/// </summary>
[Table("UserCredentials")]
public class UserCredential : Entity<Guid>
{
    /// <summary>
    /// 构造函数（用于EF Core）
    /// </summary>
    private UserCredential()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public UserCredential(
        Guid id,
        Guid projectId,
        Guid licenseInfoId,
        string username,
        string encryptedPassword)
        : base(id)
    {
        ProjectId = projectId;
        LicenseInfoId = licenseInfoId;
        Username = username;
        EncryptedPassword = encryptedPassword;
        CreatedAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>
    /// 项目ID（关联到 LicenseInfo）
    /// </summary>
    [Required]
    public Guid ProjectId { get; set; }

    [Required] public Guid LicenseInfoId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 加密后的密码（AES-256-CBC）
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string EncryptedPassword { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    [Required]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 更新密码
    /// </summary>
    public void UpdatePassword(string newEncryptedPassword)
    {
        EncryptedPassword = newEncryptedPassword;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>
    /// 更新用户名和密码
    /// </summary>
    public void UpdateCredentials(string username, string encryptedPassword)
    {
        Username = username;
        EncryptedPassword = encryptedPassword;
        UpdatedAt = DateTime.Now;
    }
}