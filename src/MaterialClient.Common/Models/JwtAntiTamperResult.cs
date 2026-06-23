namespace MaterialClient.Common.Models;

/// <summary>
///     JWT 防篡改验签结果 DTO（客户端端）。
///     与服务器端 UrbanManagement.Core.Models.JwtAntiTamperResult 结构匹配，
///     用于反序列化 SignalR Hub VerifyJwtAsync 的返回值。
/// </summary>
public class JwtAntiTamperResult
{
    /// <summary>
    ///     验签是否通过
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    ///     失败原因（Passed=true 时为 null）
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    ///     服务器从 GovProject 重新签发的 JWT（Passed=true 时有值）
    /// </summary>
    public string? ServerJwt { get; set; }

    /// <summary>
    ///     项目名称（Passed=true 时有值）
    /// </summary>
    public string? ProName { get; set; }

    /// <summary>
    ///     施工许可证号（Passed=true 时有值）
    /// </summary>
    public string? BuildLicenseNo { get; set; }

    /// <summary>
    ///     凡东对接码（Passed=true 时有值）
    /// </summary>
    public string? FdBuildLicenseNo { get; set; }

    /// <summary>
    ///     授权过期时间（Passed=true 时有值）
    /// </summary>
    public DateTime? AuthEndTime { get; set; }
}
