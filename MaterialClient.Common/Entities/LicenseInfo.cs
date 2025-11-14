using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
/// 授权许可信息实体
/// 存储软件授权信息，包括项目ID、授权令牌和有效期
/// </summary>
[Table("LicenseInfo")]
public class LicenseInfo : Entity<Guid>
{
    /// <summary>
    /// 构造函数（用于EF Core）
    /// </summary>
    private LicenseInfo()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public LicenseInfo(
        Guid id,
        Guid projectId,
        Guid? authToken,
        DateTime authEndTime,
        string machineCode)
        : base(id)
    {
        ProjectId = projectId;
        AuthToken = authToken;
        AuthEndTime = authEndTime;
        MachineCode = machineCode;
        CreatedAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>
    /// 项目ID（从基础平台获取）
    /// </summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// 授权令牌（可选，从基础平台获取）
    /// </summary>
    public Guid? AuthToken { get; set; }

    /// <summary>
    /// 授权结束时间
    /// </summary>
    [Required]
    public DateTime AuthEndTime { get; set; }

    /// <summary>
    /// 机器码（用于验证授权是否匹配当前机器）
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string MachineCode { get; set; }

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
    /// 检查授权是否已过期
    /// </summary>
    public bool IsExpired => DateTime.Now > AuthEndTime;

    /// <summary>
    /// 检查授权是否即将过期（7天内）
    /// </summary>
    public bool IsExpiringSoon => !IsExpired && (AuthEndTime - DateTime.Now).TotalDays <= 7;

    /// <summary>
    /// 更新授权信息
    /// </summary>
    public void Update(Guid? authToken, DateTime authEndTime, string machineCode)
    {
        AuthToken = authToken;
        AuthEndTime = authEndTime;
        MachineCode = machineCode;
        UpdatedAt = DateTime.Now;
    }
}

