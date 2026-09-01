using MaterialClient.Common.Entities;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Services.Urban;

/// <summary>
///     Covers Debug development authorization application on <see cref="LicenseInfo" />
///     for no-row create, invalid JWT, expired row and machineCode rewrite scenarios.
/// </summary>
public class LicenseInfoDebugAuthorizationTests
{
    private static readonly Guid DemoProjectId = Guid.Parse("08DDCF46-3744-D3E1-1999-0D645800B322");
    private static readonly DateTime DemoAuthEnd = new(2029, 10, 21, 16, 0, 0, DateTimeKind.Local);
    private const string DemoAccessCode = "XNXS20260611001";
    private const string DemoProName = "Hangzhou FanDong Demo Project";
    private const string LiveMachineCode = "live-machine-1c8237c0";

    [Fact]
    public void CreateDebugDevelopmentAuthorization_ProducesCompleteContextWithoutJwt()
    {
        var license = LicenseInfo.CreateDebugDevelopmentAuthorization(
            Guid.NewGuid(),
            DemoProjectId,
            DemoAuthEnd,
            LiveMachineCode,
            DemoProName,
            DemoAccessCode);

        AssertCompleteDevelopmentContext(license);
        license.LatestJwtToken.ShouldBeNull();
        license.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void ApplyDebugDevelopmentAuthorization_ClearsInvalidJwtAndRewritesFields()
    {
        var license = new LicenseInfo(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.Now.AddDays(-30),
            "stale-machine",
            "Old Name",
            "OLDCODE")
        {
            LatestJwtToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.invalid.signature"
        };

        license.ApplyDebugDevelopmentAuthorization(
            DemoProjectId,
            DemoAuthEnd,
            LiveMachineCode,
            DemoProName,
            DemoAccessCode);

        AssertCompleteDevelopmentContext(license);
        license.LatestJwtToken.ShouldBeNull();
        license.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void ApplyDebugDevelopmentAuthorization_ExpiredRowBecomesValid()
    {
        var license = new LicenseInfo(
            Guid.NewGuid(),
            DemoProjectId,
            DateTime.Now.AddDays(-1),
            "any",
            "x",
            "y");

        license.IsExpired.ShouldBeTrue();

        license.ApplyDebugDevelopmentAuthorization(
            DemoProjectId,
            DemoAuthEnd,
            LiveMachineCode,
            DemoProName,
            DemoAccessCode);

        license.IsExpired.ShouldBeFalse();
        license.AuthEndTime.ShouldBe(DemoAuthEnd);
    }

    [Fact]
    public void ApplyDebugDevelopmentAuthorization_MachineCodeMismatchUsesLiveValue()
    {
        var license = new LicenseInfo(
            Guid.NewGuid(),
            DemoProjectId,
            DemoAuthEnd,
            "token-machine-7ee9a79a",
            DemoProName,
            DemoAccessCode);

        license.ApplyDebugDevelopmentAuthorization(
            DemoProjectId,
            DemoAuthEnd,
            LiveMachineCode,
            DemoProName,
            DemoAccessCode);

        license.MachineCode.ShouldBe(LiveMachineCode);
    }

    private static void AssertCompleteDevelopmentContext(LicenseInfo license)
    {
        license.ProjectId.ShouldBe(DemoProjectId);
        license.ProName.ShouldBe(DemoProName);
        license.AccessCode.ShouldBe(DemoAccessCode);
        license.AuthEndTime.ShouldBe(DemoAuthEnd);
        license.MachineCode.ShouldBe(LiveMachineCode);
    }
}
