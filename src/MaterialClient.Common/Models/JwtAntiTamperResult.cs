namespace MaterialClient.Common.Models;

/// <summary>
///     防篡改验签失败类型枚举（与服务端 UrbanManagement.Core.Models.RevocationReason 对齐，
///     成员顺序保持一致以兼容 SignalR 默认的整型枚举序列化）。
///     客户端仅对 <see cref="DeviceChanged" /> 采取「清除授权 + 强制重新激活」，
///     对 <see cref="Expired" /> 采取「持久化过期 JWT + 复验后终止运行」。
/// </summary>
public enum RevocationReason
{
    /// <summary>
    ///     授权设备已变更（提交 JWT 的 machineCode 与 GovProject.MachineCode 不一致或缺失）。
    /// </summary>
    DeviceChanged,

    /// <summary>
    ///     令牌已过期。
    /// </summary>
    Expired,

    /// <summary>
    ///     服务器无此项目的记录。
    /// </summary>
    NotFound,

    /// <summary>
    ///     签名验证失败。
    /// </summary>
    InvalidSignature,

    /// <summary>
    ///     BasePlatform 不可达或签发失败。
    /// </summary>
    Unreachable
}

/// <summary>
///     JWT 防篡改验签结果 DTO（客户端端）。
///     BuildLicenseNo 为 Hub JSON wire 名（buildLicenseNo），映射到 LicenseInfo.AccessCode。
/// </summary>
public class JwtAntiTamperResult
{
    public bool Passed { get; set; }

    public string? Reason { get; set; }

    /// <summary>
    ///     失败类型枚举（Passed=true 时为 null）。客户端依据此值对设备变更做终止处理。
    /// </summary>
    public RevocationReason? RevocationReason { get; set; }

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
