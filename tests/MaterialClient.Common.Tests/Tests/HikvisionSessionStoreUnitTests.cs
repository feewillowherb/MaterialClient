using MaterialClient.Common.Services.Hikvision;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class HikvisionSessionStoreUnitTests
{
    [Fact]
    public void BuildKey_IncludesIpPortUsername()
    {
        var store = new HikvisionLoginSessionStore();
        Assert.Equal("10.0.0.1:8000:admin", store.BuildKey("10.0.0.1", 8000, "admin"));
    }

    [Fact]
    public void ProbePolicy_AllowsFirst_ThenThrottlesWithinWindow()
    {
        var now = DateTimeOffset.Parse("2026-09-16T10:00:00Z");
        Assert.True(HikvisionSessionProbePolicy.CanProbe(now, lastSucceededAt: null));

        var last = now.AddSeconds(-10);
        Assert.False(HikvisionSessionProbePolicy.CanProbe(now, last));
        Assert.True(HikvisionSessionProbePolicy.CanProbe(now.AddSeconds(30), last));
    }

    [Fact]
    public void ProbeResult_Record_CarriesFields()
    {
        var result = new HikvisionSessionProbeResult(false, 7, "CHECK_USER_STATUS failed");
        Assert.False(result.Valid);
        Assert.Equal(7u, result.ErrorCode);
        Assert.Contains("CHECK_USER_STATUS", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckUserStatus_Constant_Is20005()
    {
        Assert.Equal(20005u, HikvisionSdk.NET_DVR_CHECK_USER_STATUS);
    }
}
