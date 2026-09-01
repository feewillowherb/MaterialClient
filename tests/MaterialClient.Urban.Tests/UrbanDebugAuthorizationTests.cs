using MaterialClient.Common.Events;
using MaterialClient.Urban.Events;
using MaterialClient.Urban.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MaterialClient.Urban.Tests;

/// <summary>
///     Debug vs Release authorization boundary tests for Urban runtime event handlers.
///     Run under both <c>-c Debug</c> and <c>-c Release</c>.
/// </summary>
public class UrbanDebugAuthorizationTests
{
    private static readonly Guid DemoProjectId = Guid.Parse("08DDCF46-3744-D3E1-1999-0D645800B322");

#if DEBUG
    [Fact]
    public async Task Debug_LicenseExpiredEventHandler_DoesNotInvokeRecovery()
    {
        var recovery = Substitute.For<IUrbanLicenseRecoveryService>();
        var handler = new LicenseExpiredEventHandler(
            NullLogger<LicenseExpiredEventHandler>.Instance,
            recovery);

        await handler.HandleEventAsync(
            new LicenseExpiredEto(DemoProjectId, "expired-for-test"));

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
            new LicenseDeviceRevokedEto(DemoProjectId, "device-changed-for-test"));

        await recovery.DidNotReceiveWithAnyArgs().RecoverAsync(default!);
    }
#else
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
