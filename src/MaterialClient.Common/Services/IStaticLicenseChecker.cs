namespace MaterialClient.Common.Services;

/// <summary>
///     静态授权检查结果
/// </summary>
public class LicenseCheckResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public DateTime CheckedAt { get; init; } = DateTime.Now;

    public Guid ProId { get; init; }

    public string? ProName { get; init; }

    /// <summary>
    ///     城管接入码（来自 JWT claim accessCode）
    /// </summary>
    public string? AccessCode { get; init; }

    public DateTime AuthEndTime { get; init; }

    public static LicenseCheckResult Success(string message = "授权检查通过")
        => new() { IsSuccess = true, Message = message };

    public static LicenseCheckResult Success(
        string message,
        Guid proId,
        string? proName,
        string? accessCode,
        DateTime authEndTime)
        => new()
        {
            IsSuccess = true,
            Message = message,
            ProId = proId,
            ProName = proName,
            AccessCode = accessCode,
            AuthEndTime = authEndTime
        };

    public static LicenseCheckResult Fail(string message = "授权检查失败")
        => new() { IsSuccess = false, Message = message };
}

/// <summary>
///     静态授权检查接口
/// </summary>
public interface IStaticLicenseChecker
{
    Task<LicenseCheckResult> CheckLicenseAsync(string licenseFilePath);

    Task<LicenseCheckResult> CheckLicenseFromTokenAsync(string jwtToken);
}
