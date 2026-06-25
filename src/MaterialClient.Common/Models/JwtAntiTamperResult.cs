namespace MaterialClient.Common.Models;

/// <summary>
///     JWT 防篡改验签结果 DTO（客户端端）。
///     BuildLicenseNo 为 Hub JSON wire 名（buildLicenseNo），映射到 LicenseInfo.AccessCode。
/// </summary>
public class JwtAntiTamperResult
{
    public bool Passed { get; set; }

    public string? Reason { get; set; }

    /// <summary>
    ///     BasePlatform 签发的权威 JWT
    /// </summary>
    public string? ServerJwt { get; set; }

    public string? ProName { get; set; }

    /// <summary>
    ///     Hub JSON buildLicenseNo（城管接入码）
    /// </summary>
    public string? BuildLicenseNo { get; set; }

    public DateTime? AuthEndTime { get; set; }
}
