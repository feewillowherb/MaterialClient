namespace MaterialClient.Common.Services;

/// <summary>
///     静态授权检查结果
/// </summary>
public class LicenseCheckResult
{
    /// <summary>
    ///     授权检查是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    ///     授权检查消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    ///     检查时间
    /// </summary>
    public DateTime CheckedAt { get; init; } = DateTime.Now;

    /// <summary>
    ///     项目ID
    /// </summary>
    public Guid ProId { get; init; }

    /// <summary>
    ///     项目名称
    /// </summary>
    public string? ProName { get; init; }

    /// <summary>
    ///     施工许可证号
    /// </summary>
    public string? BuildLicenseNo { get; init; }

    /// <summary>
    ///     对接码
    /// </summary>
    public string? FdBuildLicenseNo { get; init; }

    /// <summary>
    ///     授权过期时间
    /// </summary>
    public DateTime AuthEndTime { get; init; }

    /// <summary>
    ///     创建成功结果（无授权数据）
    /// </summary>
    public static LicenseCheckResult Success(string message = "授权检查通过")
        => new() { IsSuccess = true, Message = message };

    /// <summary>
    ///     创建成功结果（携带授权数据）
    /// </summary>
    public static LicenseCheckResult Success(
        string message,
        Guid proId,
        string? proName,
        string? buildLicenseNo,
        string? fdBuildLicenseNo,
        DateTime authEndTime)
        => new()
        {
            IsSuccess = true,
            Message = message,
            ProId = proId,
            ProName = proName,
            BuildLicenseNo = buildLicenseNo,
            FdBuildLicenseNo = fdBuildLicenseNo,
            AuthEndTime = authEndTime
        };

    /// <summary>
    ///     创建失败结果
    /// </summary>
    public static LicenseCheckResult Fail(string message = "授权检查失败")
        => new() { IsSuccess = false, Message = message };
}

/// <summary>
///     静态授权检查接口
///     用于 Urban 模式启动时的后台授权验证
///     TODO: 当前实现默认返回成功，后续完善实际授权逻辑
/// </summary>
public interface IStaticLicenseChecker
{
    /// <summary>
    ///     检查授权文件
    /// </summary>
    /// <param name="licenseFilePath">授权文件路径</param>
    /// <returns>授权检查结果</returns>
    Task<LicenseCheckResult> CheckLicenseAsync(string licenseFilePath);
}
