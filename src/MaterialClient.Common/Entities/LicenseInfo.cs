using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

/// <summary>
///     授权许可信息实体
///     存储软件授权信息，包括项目ID、接入码和有效期
/// </summary>
[Table("LicenseInfo")]
public class LicenseInfo : Entity<Guid>
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
        CreatedAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
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

    [Required]
    public DateTime CreatedAt { get; set; }

    [Required]
    public DateTime UpdatedAt { get; set; }

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
        UpdatedAt = DateTime.Now;
    }
}
