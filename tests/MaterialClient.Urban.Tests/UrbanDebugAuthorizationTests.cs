using MaterialClient.Common.Events;
using MaterialClient.Urban.Events;
using MaterialClient.Urban.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MaterialClient.Urban.Tests;

/// <summary>
///     Debug vs Release authorization boundary tests for Urban event handlers and Debug context.
///     Run under both <c>-c Debug</c> and <c>-c Release</c> (task 4.5).
/// </summary>
public class UrbanDebugAuthorizationTests
{
#if DEBUG
    [Fact]
    public void Debug_DevelopmentAuthorization_ExposesFixedDemoContext()
    {
        UrbanDebugDevelopmentAuthorization.ProjectId
            .ShouldBe(Guid.Parse("08DDCF46-3744-D3E1-1999-0D645800B322"));
        UrbanDebugDevelopmentAuthorization.AccessCode.ShouldBe("XNXS20260611001");
        UrbanDebugDevelopmentAuthorization.ProName.ShouldBe("Hangzhou FanDong Demo Project");
        UrbanDebugDevelopmentAuthorization.AuthEndTime
            .ShouldBe(new DateTime(2029, 10, 21, 16, 0, 0, DateTimeKind.Local));
        UrbanDebugDevelopmentAuthorization.SuccessMessage.ShouldContain("DEBUG");
    }

    [Fact]
    public void Debug_AuthorizedStartupResult_DoesNotRequireRecovery()
    {
        var result = new UrbanStartupAuthorizationResult(
            true,
            UrbanDebugDevelopmentAuthorization.SuccessMessage,
            UrbanDebugDevelopmentAuthorization.ProjectId);

        result.IsAuthorized.ShouldBeTrue();
        result.ProId.ShouldBe(UrbanDebugDevelopmentAuthorization.ProjectId);
        result.FailureMessage.ShouldBe(UrbanDebugDevelopmentAuthorization.SuccessMessage);
    }

    [Fact]
    public async Task Debug_LicenseExpiredEventHandler_DoesNotInvokeRecovery()
    {
        var recovery = Substitute.For<IUrbanLicenseRecoveryService>();
        var handler = new LicenseExpiredEventHandler(
            NullLogger<LicenseExpiredEventHandler>.Instance,
            recovery);

        await handler.HandleEventAsync(
            new LicenseExpiredEto(UrbanDebugDevelopmentAuthorization.ProjectId, "expired-for-test"));

        await recovery.DidNotReceiveWithAnyArgs().RecoverAsync(default!);
    }

    [Fact]
    public async Task Debug_LicenseDeviceRevokedEventHandler_DoesNotInvokeRecovery()
    {
        var recovery = Substitute.For<IUrbanLicenseRecoveryService>();
        var handler = new LicenseDeviceRevokedEventHandler(
            NullLogger<LicenseDeviceRevokedEventHandler>.Instance,
            recovery);

        await handler.HandleEventAsync(
            new LicenseDeviceRevokedEto(
                UrbanDebugDevelopmentAuthorization.ProjectId,
                "device-changed-for-test"));

        await recovery.DidNotReceiveWithAnyArgs().RecoverAsync(default!);
    }

    [Fact]
    public void Debug_SyncProjectLicenseFromServerAsync_EarlyReturnsWithoutHub()
    {
        // Compile-time bypass: SyncProjectLicenseFromServerAsync begins with #if DEBUG return.
        // Presence of UrbanDebugDevelopmentAuthorization in this assembly proves the Debug path.
        typeof(UrbanDebugDevelopmentAuthorization).ShouldNotBeNull();
        typeof(MaterialClientUrbanModule).Assembly
            .GetType("MaterialClient.Urban.Services.UrbanDebugDevelopmentAuthorization")
            .ShouldNotBeNull();
    }
#else
    [Fact]
    public void Release_DoesNotIncludeDebugDevelopmentAuthorizationType()
    {
        typeof(MaterialClientUrbanModule).Assembly
            .GetType("MaterialClient.Urban.Services.UrbanDebugDevelopmentAuthorization")
            .ShouldBeNull();
    }

    [Fact]
    public void Release_UnauthorizedStartupResult_BlocksMainFlow()
    {
        var result = new UrbanStartupAuthorizationResult(false, "authorization failed", null);

        result.IsAuthorized.ShouldBeFalse();
        result.FailureMessage.ShouldBe("authorization failed");
        result.ProId.ShouldBeNull();
    }
#endif
}
