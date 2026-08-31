using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using MaterialClient.Common.Services.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     静态授权检查服务（JWT 实现）
/// </summary>
public class StaticLicenseChecker : IStaticLicenseChecker, ISingletonDependency
{
    private const string ValidIssuer = "BasePlatform";
    private const string ValidAudience = "MaterialClient.Urban";

    private readonly IConfiguration _configuration;
    private readonly IMachineCodeService _machineCodeService;
    private readonly ILogger<StaticLicenseChecker> _logger;
    private readonly SecurityKey? _signingKey;
    private readonly bool _keyConfigured;

    public StaticLicenseChecker(
        IConfiguration configuration,
        IMachineCodeService machineCodeService,
        ILogger<StaticLicenseChecker> logger)
    {
        _configuration = configuration;
        _machineCodeService = machineCodeService;
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

            _logger.LogInformation("JWT 公钥加载成功（验签 iss={Issuer}）", ValidIssuer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JWT 公钥格式无效，无法解析 PEM");
            _keyConfigured = false;
        }
    }

    public async Task<LicenseCheckResult> CheckLicenseFromTokenAsync(string jwtToken)
    {
        await Task.CompletedTask;

        try
        {
            if (string.IsNullOrWhiteSpace(jwtToken))
            {
                return LicenseCheckResult.Fail("JWT 令牌为空");
            }

            if (!_keyConfigured || _signingKey == null)
            {
                _logger.LogWarning("JWT 公钥未配置或无效，无法验证授权");
                return LicenseCheckResult.Fail("授权公钥未配置，无法验证");
            }

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(
                jwtToken.Trim(),
                CreateValidationParameters(),
                out var validatedToken);

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

    public async Task<LicenseCheckResult> CheckLicenseAsync(string licenseFilePath)
    {
        try
        {
            _logger.LogInformation("开始 JWT 授权检查: LicenseFilePath={Path}", licenseFilePath);

            if (!File.Exists(licenseFilePath))
            {
                _logger.LogWarning("授权文件不存在: {Path}", licenseFilePath);
                return LicenseCheckResult.Fail("未授权");
            }

            var token = await File.ReadAllTextAsync(licenseFilePath);
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("授权文件内容为空: {Path}", licenseFilePath);
                return LicenseCheckResult.Fail("授权文件内容不是有效的 JWT 令牌");
            }

            return await CheckLicenseFromTokenAsync(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JWT 授权检查异常: {Message}", ex.Message);
            return LicenseCheckResult.Fail($"授权检查异常: {ex.Message}");
        }
    }

    private TokenValidationParameters CreateValidationParameters()
        => new()
        {
            ValidateIssuer = true,
            ValidIssuer = ValidIssuer,
            ValidateAudience = true,
            ValidAudience = ValidAudience,
            ValidateLifetime = true,
            IssuerSigningKey = _signingKey,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };

    private LicenseCheckResult ExtractClaimsFromPrincipal(System.Security.Claims.ClaimsPrincipal principal)
    {
        var claims = principal.Claims;

        var proIdStr = claims.FirstOrDefault(c => c.Type == "proId")?.Value;
        var proName = claims.FirstOrDefault(c => c.Type == "proName")?.Value;
        var accessCode = claims.FirstOrDefault(c => c.Type == "accessCode")?.Value;
        var machineCodeClaim = claims.FirstOrDefault(c => c.Type == "machineCode")?.Value;
        var expClaim = claims.FirstOrDefault(c => c.Type == "exp")?.Value;

        if (string.IsNullOrWhiteSpace(proIdStr) || !Guid.TryParse(proIdStr, out var proId))
        {
            _logger.LogWarning("JWT 令牌中缺少有效的 proId claim");
            return LicenseCheckResult.Fail("授权数据不完整: 缺少项目ID");
        }

        if (string.IsNullOrWhiteSpace(accessCode))
        {
            _logger.LogWarning("JWT 令牌中缺少 accessCode claim");
            return LicenseCheckResult.Fail("授权数据不完整: 缺少接入码");
        }

        var localMachineCode = _machineCodeService.GetMachineCode();
        if (string.IsNullOrWhiteSpace(machineCodeClaim) ||
            !string.Equals(machineCodeClaim, localMachineCode, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "JWT machineCode 与本机不一致: Claim={Claim}, Local={Local}",
                machineCodeClaim,
                localMachineCode);
            return LicenseCheckResult.Fail("授权机器码与当前设备不匹配");
        }

        DateTime authEndTime = default;
        if (long.TryParse(expClaim, out var expUnix))
        {
            authEndTime = DateTimeOffset.FromUnixTimeSeconds(expUnix).DateTime;
        }

        var successMessage = $"授权检查通过，过期时间: {authEndTime:yyyy-MM-dd}";
        _logger.LogInformation(
            "JWT 授权检查完成: Result=Success, ProId={ProId}, ProName={ProName}, AccessCode={AccessCode}, AuthEndTime={AuthEndTime}",
            proId, proName, accessCode, authEndTime);

        return LicenseCheckResult.Success(successMessage, proId, proName, accessCode, authEndTime);
    }
}
