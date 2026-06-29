using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;

namespace MaterialClient.Common.Entities;

/// <summary>
///     授权许可信息实体
///     存储软件授权信息，包括项目ID、接入码和有效期。
///     继承 <see cref="AuditedEntity{TKey}" />，CreationTime/LastModificationTime 等审计字段
///     由 ABP（AbpDbContext）在保存时自动填充，无需手动赋值（F3）。
/// </summary>
[Table("LicenseInfo")]
public class LicenseInfo : AuditedEntity<Guid>
{
    private LicenseInfo()
    {
    }

    public LicenseInfo(
        Guid id,
        Guid projectId,
        DateTime authEndTime,
        string machineCode,
        string? proName = null,
        string? accessCode = null)
        : base(id)
    {
        ProjectId = projectId;
        AuthEndTime = authEndTime;
        MachineCode = machineCode;
        ProName = proName;
        AccessCode = accessCode;
        // CreationTime/LastModificationTime are auto-filled by ABP on save.
    }

    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    public DateTime AuthEndTime { get; set; }

    [MaxLength(256)]
    public string? ProName { get; set; }

    /// <summary>
    ///     城管接入码（与 BasePlatform accessCode / 政府协议 buildLicenseNo 对应）
    /// </summary>
    [MaxLength(128)]
    public string? AccessCode { get; set; }

    [MaxLength(4096)]
    public string? LatestJwtToken { get; set; }

    [Required]
    [MaxLength(128)]
    public string? MachineCode { get; set; }

    public bool IsExpired => DateTime.Now > AuthEndTime;

    public bool IsExpiringSoon => !IsExpired && (AuthEndTime - DateTime.Now).TotalDays <= 7;

    public void Update(
        DateTime authEndTime,
        string machineCode,
        string? proName = null,
        string? accessCode = null)
    {
        AuthEndTime = authEndTime;
        MachineCode = machineCode;
        ProName = proName;
        AccessCode = accessCode;
        // LastModificationTime is auto-filled by ABP on update.
    }
}

