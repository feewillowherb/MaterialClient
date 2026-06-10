using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     静态授权检查服务
///     从 RSA.xml 文件中读取加密授权数据，使用 RSA 私钥解密验证
/// </summary>
public class StaticLicenseChecker : IStaticLicenseChecker, ISingletonDependency
{
    private readonly ILogger<StaticLicenseChecker>? _logger;

    public StaticLicenseChecker(ILogger<StaticLicenseChecker>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     检查授权文件
    ///     从 RSA.xml 文件读取并解密授权数据，验证授权有效期
    /// </summary>
    /// <param name="licenseFilePath">授权文件路径（RSA.xml）</param>
    /// <returns>授权检查结果</returns>
    public async Task<LicenseCheckResult> CheckLicenseAsync(string licenseFilePath)
    {
        await Task.CompletedTask;

        try
        {
            _logger?.LogInformation("开始静态授权检查: LicenseFilePath={Path}", licenseFilePath);

            // 2.2 文件缺失场景
            if (!File.Exists(licenseFilePath))
            {
                _logger?.LogWarning("授权文件不存在: {Path}", licenseFilePath);
                return LicenseCheckResult.Fail("授权文件不存在");
            }

            // 调用 RsaLicenseDecryptor 读取并解密授权数据
            var decryptResult = RsaLicenseDecryptor.ReadAndDecrypt(licenseFilePath);

            // 2.3 授权过期场景
            if (decryptResult.IsExpired)
            {
                var expiredMessage =
                    $"授权已过期，过期时间: {decryptResult.AuthEndTime:yyyy-MM-dd}，已过期 {Math.Abs(decryptResult.DaysRemaining)} 天";
                _logger?.LogWarning("授权已过期: AuthEndTime={AuthEndTime}, DaysOverdue={DaysOverdue}",
                    decryptResult.AuthEndTime, Math.Abs(decryptResult.DaysRemaining));
                return LicenseCheckResult.Fail(expiredMessage);
            }

            // 2.4 授权有效场景
            var successMessage =
                $"授权检查通过，过期时间: {decryptResult.AuthEndTime:yyyy-MM-dd}，剩余 {decryptResult.DaysRemaining} 天";
            _logger?.LogInformation(
                "静态授权检查完成: Result=Success, ProId={ProId}, BuildLicenseNo={BuildLicenseNo}, AuthEndTime={AuthEndTime}, DaysRemaining={DaysRemaining}",
                decryptResult.ProId, decryptResult.BuildLicenseNo, decryptResult.AuthEndTime,
                decryptResult.DaysRemaining);

            return LicenseCheckResult.Success(
                successMessage,
                decryptResult.ProId,
                null, // ProName: RSA.xml 中不存在，保持为 null
                decryptResult.BuildLicenseNo,
                null, // FdBuildLicenseNo: RSA.xml 中不存在，保持为 null
                decryptResult.AuthEndTime);
        }
        // 2.5 异常兜底
        catch (Exception ex)
        {
            _logger?.LogError(ex, "静态授权检查异常: {Message}", ex.Message);
            return LicenseCheckResult.Fail($"授权检查异常: {ex.Message}");
        }
    }
}
