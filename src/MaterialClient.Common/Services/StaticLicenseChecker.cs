using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     静态授权检查服务
///     硬编码测试授权数据，返回固定的 ProId/ProName/BuildLicenseNo/FdBuildLicenseNo
///     TODO: 当前实现返回硬编码测试数据，后续完善实际授权逻辑
/// </summary>
public class StaticLicenseChecker : IStaticLicenseChecker, ISingletonDependency
{
    private readonly ILogger<StaticLicenseChecker>? _logger;

    /// <summary>
    ///     固定测试项目 ID（与 LicenseService.VerifyAuthorizationCodeTestAsync 的 testProjectId 一致）
    /// </summary>
    private static readonly Guid TestProId = Guid.Parse("C7F4F03C-4ED2-40FE-8898-D79331A3942D");

    /// <summary>
    ///     固定测试项目名称
    /// </summary>
    private const string TestProName = "测试项目-StaticLicense";

    /// <summary>
    ///     固定测试施工许可证号
    /// </summary>
    private const string TestBuildLicenseNo = "TEST-BUILD-LICENSE-001";

    /// <summary>
    ///     固定测试对接码
    /// </summary>
    private const string TestFdBuildLicenseNo = "TEST-FD-BUILD-LICENSE-001";

    public StaticLicenseChecker(ILogger<StaticLicenseChecker>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     检查授权文件
    ///     当前实现返回硬编码测试数据，与 LicenseService.VerifyAuthorizationCodeTestAsync 模式一致
    /// </summary>
    /// <param name="licenseFilePath">授权文件路径</param>
    /// <returns>授权检查结果（含硬编码测试数据）</returns>
    public async Task<LicenseCheckResult> CheckLicenseAsync(string licenseFilePath)
    {
        await Task.CompletedTask;

        try
        {
            _logger?.LogInformation("开始静态授权检查: LicenseFilePath={Path}", licenseFilePath);

            var fileExists = File.Exists(licenseFilePath);
            if (!fileExists)
            {
                _logger?.LogDebug("授权文件不存在，使用硬编码测试数据: {Path}", licenseFilePath);
            }

            var testAuthEndTime = DateTime.Now.AddYears(1);

            _logger?.LogInformation(
                "静态授权检查完成: FileExists={Exists}, Result=Success, ProId={ProId}, ProName={ProName}, AuthEndTime={AuthEndTime}",
                fileExists, TestProId, TestProName, testAuthEndTime);

            return LicenseCheckResult.Success(
                "授权检查通过（静态测试数据）",
                TestProId,
                TestProName,
                TestBuildLicenseNo,
                TestFdBuildLicenseNo,
                testAuthEndTime);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "静态授权检查异常");
            return LicenseCheckResult.Fail($"授权检查异常: {ex.Message}");
        }
    }
}
