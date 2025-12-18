namespace MaterialClient.Common.Api.Dtos;

/// <summary>
/// 授权信息DTO
/// </summary>
public class LicenseInfoDto
{
    /// <summary>
    /// 项目ID
    /// </summary>
    public Guid Proid { get; set; }

    /// <summary>
    /// 授权Token
    /// </summary>
    public Guid? AuthToken { get; set; }

    /// <summary>
    /// 授权到期时间
    /// </summary>
    public DateTime AuthEndTime { get; set; }

    /// <summary>
    /// 机器码
    /// </summary>
    public string? MachineCode { get; set; }
}