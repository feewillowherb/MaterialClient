using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Services.Urban;

/// <summary>
///     Proves <see cref="StaticLicenseChecker" /> stays strict for missing, malformed, expired,
///     invalid-signature, missing-claim and machineCode-mismatched tokens.
/// </summary>
public sealed class StaticLicenseCheckerTests : IDisposable
{
    private const string LocalMachineCode = "local-machine-code-abc";
    private readonly RSA _rsa;
    private readonly string _publicKeyPem;
    private readonly string _privateKeyPem;
    private readonly IMachineCodeService _machineCodeService;
    private readonly StaticLicenseChecker _checker;
    private readonly string _tempDir;

    public StaticLicenseCheckerTests()
    {
        _rsa = RSA.Create(2048);
        _publicKeyPem = _rsa.ExportSubjectPublicKeyInfoPem();
        _privateKeyPem = _rsa.ExportPkcs8PrivateKeyPem();
        _tempDir = Path.Combine(Path.GetTempPath(), "StaticLicenseCheckerTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _machineCodeService = Substitute.For<IMachineCodeService>();
        _machineCodeService.GetMachineCode().Returns(LocalMachineCode);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:PublicKey"] = _publicKeyPem
            })
            .Build();

        _checker = new StaticLicenseChecker(
            configuration,
            _machineCodeService,
            NullLogger<StaticLicenseChecker>.Instance);
    }

    public void Dispose()
    {
        _rsa.Dispose();
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }

    [Fact]
    public async Task CheckLicenseFromTokenAsync_ValidToken_ReturnsSuccess()
    {
        var proId = Guid.Parse("08DDCF46-3744-D3E1-1999-0D645800B322");
        var token = CreateSignedJwt(
            proId: proId,
            accessCode: "XNXS20260611001",
            machineCode: LocalMachineCode,
            expires: DateTime.UtcNow.AddDays(30));

        var result = await _checker.CheckLicenseFromTokenAsync(token);

        result.IsSuccess.ShouldBeTrue(result.Message);
        result.ProId.ShouldBe(proId);
        result.AccessCode.ShouldBe("XNXS20260611001");
    }

    [Fact]
    public async Task CheckLicenseFromTokenAsync_MissingToken_ReturnsFail()
    {
        var result = await _checker.CheckLicenseFromTokenAsync("   ");

        result.IsSuccess.ShouldBeFalse();
        result.Message.ShouldContain("JWT");
    }

    [Fact]
    public async Task CheckLicenseFromTokenAsync_MalformedToken_ReturnsFail()
    {
        var result = await _checker.CheckLicenseFromTokenAsync("not-a-jwt");

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckLicenseFromTokenAsync_ExpiredToken_ReturnsFail()
    {
        var token = CreateSignedJwt(
            proId: Guid.NewGuid(),
            accessCode: "ACCESS",
            machineCode: LocalMachineCode,
            expires: DateTime.UtcNow.AddHours(-2));

        var result = await _checker.CheckLicenseFromTokenAsync(token);

        result.IsSuccess.ShouldBeFalse();
        result.Message.ShouldContain("过期");
    }

    [Fact]
    public async Task CheckLicenseFromTokenAsync_InvalidSignature_ReturnsFail()
    {
        using var otherRsa = RSA.Create(2048);
        var token = CreateSignedJwt(
            proId: Guid.NewGuid(),
            accessCode: "ACCESS",
            machineCode: LocalMachineCode,
            expires: DateTime.UtcNow.AddDays(1),
            signingKeyPem: otherRsa.ExportPkcs8PrivateKeyPem());

        var result = await _checker.CheckLicenseFromTokenAsync(token);

        result.IsSuccess.ShouldBeFalse();
        result.Message.ShouldContain("签名");
    }

    [Fact]
    public async Task CheckLicenseFromTokenAsync_MissingProId_ReturnsFail()
    {
        var token = CreateSignedJwt(
            proId: null,
            accessCode: "ACCESS",
            machineCode: LocalMachineCode,
            expires: DateTime.UtcNow.AddDays(1));

        var result = await _checker.CheckLicenseFromTokenAsync(token);

        result.IsSuccess.ShouldBeFalse();
        result.Message.ShouldContain("项目ID");
    }

    [Fact]
    public async Task CheckLicenseFromTokenAsync_MissingAccessCode_ReturnsFail()
    {
        var token = CreateSignedJwt(
            proId: Guid.NewGuid(),
            accessCode: null,
            machineCode: LocalMachineCode,
            expires: DateTime.UtcNow.AddDays(1));

        var result = await _checker.CheckLicenseFromTokenAsync(token);

        result.IsSuccess.ShouldBeFalse();
        result.Message.ShouldContain("接入码");
    }

    [Fact]
    public async Task CheckLicenseFromTokenAsync_MachineCodeMismatch_BehaviorDependsOnBuild()
    {
        var token = CreateSignedJwt(
            proId: Guid.NewGuid(),
            accessCode: "ACCESS",
            machineCode: "other-machine",
            expires: DateTime.UtcNow.AddDays(1));

        var result = await _checker.CheckLicenseFromTokenAsync(token);

#if DEBUG
        result.IsSuccess.ShouldBeTrue();
#else
        result.IsSuccess.ShouldBeFalse();
        result.Message.ShouldContain("机器码");
#endif
    }

    [Fact]
    public async Task CheckLicenseAsync_MissingFile_ReturnsFail()
    {
        var missingPath = Path.Combine(_tempDir, "missing.urban");

        var result = await _checker.CheckLicenseAsync(missingPath);

        result.IsSuccess.ShouldBeFalse();
        result.Message.ShouldBe("未授权");
    }

    [Fact]
    public async Task CheckLicenseAsync_FileWithValidToken_ReturnsSuccess()
    {
        var token = CreateSignedJwt(
            proId: Guid.NewGuid(),
            accessCode: "ACCESS",
            machineCode: LocalMachineCode,
            expires: DateTime.UtcNow.AddDays(1));
        var path = Path.Combine(_tempDir, "license.urban");
        await File.WriteAllTextAsync(path, token);

        var result = await _checker.CheckLicenseAsync(path);

        result.IsSuccess.ShouldBeTrue(result.Message);
    }

    [Fact]
    public async Task CheckLicenseFromTokenAsync_PublicKeyMissing_ReturnsFail()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var checker = new StaticLicenseChecker(
            configuration,
            _machineCodeService,
            NullLogger<StaticLicenseChecker>.Instance);

        var result = await checker.CheckLicenseFromTokenAsync("header.payload.sig");

        result.IsSuccess.ShouldBeFalse();
        result.Message.ShouldContain("公钥");
    }

    private string CreateSignedJwt(
        Guid? proId,
        string? accessCode,
        string? machineCode,
        DateTime expires,
        string? signingKeyPem = null)
    {
        using var signingRsa = RSA.Create();
        signingRsa.ImportFromPem(signingKeyPem ?? _privateKeyPem);
        var credentials = new SigningCredentials(
            new RsaSecurityKey(signingRsa),
            SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>();
        if (proId.HasValue)
        {
            claims.Add(new Claim("proId", proId.Value.ToString()));
            claims.Add(new Claim("proName", "Test Project"));
        }

        if (accessCode != null)
        {
            claims.Add(new Claim("accessCode", accessCode));
        }

        if (machineCode != null)
        {
            claims.Add(new Claim("machineCode", machineCode));
        }

        var notBefore = expires.AddHours(-24);
        if (notBefore > DateTime.UtcNow.AddHours(-1))
        {
            notBefore = DateTime.UtcNow.AddHours(-1);
        }

        var token = new JwtSecurityToken(
            issuer: "BasePlatform",
            audience: "MaterialClient.Urban",
            claims: claims,
            notBefore: notBefore,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LicenseCheckResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        var result = LicenseCheckResult.Success("ok");
        result.IsSuccess.ShouldBeTrue();
        result.Message.ShouldBe("ok");
    }

    [Fact]
    public void Fail_CreatesFailedResult()
    {
        var result = LicenseCheckResult.Fail("denied");
        result.IsSuccess.ShouldBeFalse();
        result.Message.ShouldBe("denied");
    }

    [Fact]
    public void Success_WithClaims_PopulatesFields()
    {
        var proId = Guid.NewGuid();
        var end = new DateTime(2029, 10, 21);
        var result = LicenseCheckResult.Success("ok", proId, "name", "code", end);

        result.IsSuccess.ShouldBeTrue();
        result.ProId.ShouldBe(proId);
        result.ProName.ShouldBe("name");
        result.AccessCode.ShouldBe("code");
        result.AuthEndTime.ShouldBe(end);
    }
}
