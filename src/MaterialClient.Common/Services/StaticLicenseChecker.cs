using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     静态授权检查服务（JWT 实现）
///     从 .urban 文件中读取 JWT 令牌，使用 RSA 公钥验证 RS256 签名，提取 Claims
/// </summary>
public class StaticLicenseChecker : IStaticLicenseChecker, ISingletonDependency
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StaticLicenseChecker> _logger;

    private readonly SecurityKey? _signingKey;
    private readonly bool _keyConfigured;

    public StaticLicenseChecker(IConfiguration configuration, ILogger<StaticLicenseChecker> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var publicKeyPem = _configuration["Jwt:PublicKey"];

        if (string.IsNullOrWhiteSpace(publicKeyPem))
        {
            _logger.LogWarning("JWT 公钥未配置 (Jwt:PublicKey)，后续授权检查将失败");
            _keyConfigured = false;
            return;
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);

            _signingKey = new RsaSecurityKey(rsa.ExportParameters(false));
            _keyConfigured = true;

            _logger.LogInformation("JWT 公钥加载成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JWT 公钥格式无效，无法解析 PEM");
            _keyConfigured = false;
        }
    }

    /// <inheritdoc />
    public async Task<LicenseCheckResult> CheckLicenseFromTokenAsync(string jwtToken)
    {
        await Task.CompletedTask;

        try
        {
            if (string.IsNullOrWhiteSpace(jwtToken))
            {
                return LicenseCheckResult.Fail("JWT 令牌为空");
            }

            var token = jwtToken.Trim();

            if (!_keyConfigured || _signingKey == null)
            {
                _logger.LogWarning("JWT 公钥未配置或无效，无法验证授权");
                return LicenseCheckResult.Fail("授权公钥未配置，无法验证");
            }

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "UrbanManagement",
                ValidateAudience = true,
                ValidAudience = "MaterialClient.Urban",
                ValidateLifetime = true,
                IssuerSigningKey = _signingKey,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken)
            {
                _logger.LogWarning("令牌验证结果不是有效的 JWT");
                return LicenseCheckResult.Fail("授权验证失败");
            }

            return ExtractClaimsFromPrincipal(principal);
        }
        catch (SecurityTokenExpiredException ex)
        {
            var expiresAt = ex.Expires.ToString("yyyy-MM-dd");
            _logger.LogWarning("JWT 授权已过期: Expires={Expires}", expiresAt);
            return LicenseCheckResult.Fail($"授权已过期，过期时间: {expiresAt}");
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            _logger.LogWarning(ex, "JWT 签名验证失败");
            return LicenseCheckResult.Fail("授权签名验证失败");
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.LogWarning(ex, "JWT 验证失败: {Message}", ex.Message);
            return LicenseCheckResult.Fail($"授权验证失败: {ex.Message}");
        }
        catch (FormatException)
        {
            _logger.LogWarning("JWT 令牌不是有效的 JWT 格式");
            return LicenseCheckResult.Fail("授权内容不是有效的 JWT 令牌");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JWT 授权检查异常: {Message}", ex.Message);
            return LicenseCheckResult.Fail($"授权检查异常: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<LicenseCheckResult> CheckLicenseAsync(string licenseFilePath)
    {
        await Task.CompletedTask;

        try
        {
            _logger.LogInformation("开始 JWT 授权检查: LicenseFilePath={Path}", licenseFilePath);

            // 文件不存在
            if (!File.Exists(licenseFilePath))
            {
                _logger.LogWarning("授权文件不存在: {Path}", licenseFilePath);
                return LicenseCheckResult.Fail("授权文件未找到");
            }

            // 公钥未配置
            if (!_keyConfigured || _signingKey == null)
            {
                _logger.LogWarning("JWT 公钥未配置或无效，无法验证授权");
                return LicenseCheckResult.Fail("授权公钥未配置，无法验证");
            }

            var token = await File.ReadAllTextAsync(licenseFilePath);

            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("授权文件内容为空: {Path}", licenseFilePath);
                return LicenseCheckResult.Fail("授权文件内容不是有效的 JWT 令牌");
            }

            token = token.Trim();

            // 配置验证参数
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "UrbanManagement",
                ValidateAudience = true,
                ValidAudience = "MaterialClient.Urban",
                ValidateLifetime = true,
                IssuerSigningKey = _signingKey,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var handler = new JwtSecurityTokenHandler();

            // 验证令牌
            var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken)
            {
                _logger.LogWarning("令牌验证结果不是有效的 JWT");
                return LicenseCheckResult.Fail("授权验证失败");
            }

            // 提取 Claims
            return ExtractClaimsFromPrincipal(principal);
        }
        catch (SecurityTokenExpiredException ex)
        {
            var expiresAt = ex.Expires.ToString("yyyy-MM-dd");
            _logger.LogWarning("JWT 授权已过期: Expires={Expires}", expiresAt);
            return LicenseCheckResult.Fail($"授权已过期，过期时间: {expiresAt}");
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            _logger.LogWarning(ex, "JWT 签名验证失败");
            return LicenseCheckResult.Fail("授权签名验证失败");
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.LogWarning(ex, "JWT 验证失败: {Message}", ex.Message);
            return LicenseCheckResult.Fail($"授权验证失败: {ex.Message}");
        }
        catch (FormatException)
        {
            _logger.LogWarning("授权文件内容不是有效的 JWT 格式: {Path}", licenseFilePath);
            return LicenseCheckResult.Fail("授权文件内容不是有效的 JWT 令牌");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JWT 授权检查异常: {Message}", ex.Message);
            return LicenseCheckResult.Fail($"授权检查异常: {ex.Message}");
        }
    }

    /// <summary>
    ///     从已验证的 ClaimsPrincipal 提取授权信息
    /// </summary>
    private LicenseCheckResult ExtractClaimsFromPrincipal(System.Security.Claims.ClaimsPrincipal principal)
    {
        var claims = principal.Claims;

        var proIdStr = claims.FirstOrDefault(c => c.Type == "proId")?.Value;
        var proName = claims.FirstOrDefault(c => c.Type == "proName")?.Value;
        var buildLicenseNo = claims.FirstOrDefault(c => c.Type == "buildLicenseNo")?.Value;
        var fdBuildLicenseNo = claims.FirstOrDefault(c => c.Type == "fdBuildLicenseNo")?.Value;
        var expClaim = claims.FirstOrDefault(c => c.Type == "exp")?.Value;

        if (string.IsNullOrWhiteSpace(proIdStr) || !Guid.TryParse(proIdStr, out var proId))
        {
            _logger.LogWarning("JWT 令牌中缺少有效的 proId claim");
            return LicenseCheckResult.Fail("授权数据不完整: 缺少项目ID");
        }

        // 从 exp claim 解析过期时间
        DateTime authEndTime = default;
        if (long.TryParse(expClaim, out var expUnix))
        {
            authEndTime = DateTimeOffset.FromUnixTimeSeconds(expUnix).DateTime;
        }

        var successMessage = $"授权检查通过，过期时间: {authEndTime:yyyy-MM-dd}";
        _logger.LogInformation(
            "JWT 授权检查完成: Result=Success, ProId={ProId}, ProName={ProName}, BuildLicenseNo={BuildLicenseNo}, AuthEndTime={AuthEndTime}",
            proId, proName, buildLicenseNo, authEndTime);

        return LicenseCheckResult.Success(
            successMessage,
            proId,
            proName,
            buildLicenseNo,
            fdBuildLicenseNo,
            authEndTime);
    }
}
