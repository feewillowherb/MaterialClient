using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     静态授权检查服务
///     TODO: 当前实现默认返回成功，不进行实际授权验证，后续完善实际授权逻辑
///     首期实现仅记录日志到文件，不进行实际授权验证
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
    ///     TODO: 当前实现默认返回成功，后续完善实际授权逻辑
    /// </summary>
    /// <param name="licenseFilePath">授权文件路径</param>
    /// <returns>授权检查结果</returns>
    public async Task<LicenseCheckResult> CheckLicenseAsync(string licenseFilePath)
    {
        await Task.CompletedTask;

        try
        {
            _logger?.LogInformation("开始静态授权检查: LicenseFilePath={Path}", licenseFilePath);

            // TODO: 后续实现实际授权验证逻辑
            // 1. 读取 licenseFilePath 指定的授权文件
            // 2. 验证授权文件签名和有效期
            // 3. 提取授权信息（设备绑定、功能权限等）
            // 当前实现默认返回成功
            var fileExists = File.Exists(licenseFilePath);
            _logger?.LogInformation("静态授权检查完成: FileExists={Exists}, Result=Success (TODO: 默认成功)", fileExists);

            return LicenseCheckResult.Success("授权检查通过（默认成功 - TODO: 后续实现实际验证）");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "静态授权检查异常");
            // 授权检查失败不阻止应用启动，仅记录日志
            return LicenseCheckResult.Fail($"授权检查异常: {ex.Message}");
        }
    }
}
