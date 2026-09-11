using MaterialClient.Common.Configuration;
using MaterialClient.Common.Services.Hikvision;
using Xunit;

namespace MaterialClient.Common.Tests;

public class HikvisionSoftResetAndTimeoutDiagnosticsTests
{
    [Fact]
    public void TimeoutDiagnostics_NoPort_ReportsNoSysHead_AndDoesNotBlameError32()
    {
        var info = StreamCaptureTimeoutDiagnostics.Classify(
            decoderPort: -1,
            isInitialized: false,
            playM4ErrorFromValidPort: 32);

        Assert.Equal(StreamCaptureTimeoutDiagnostics.ReasonNoSysHead, info.Reason);
        Assert.Equal(0, info.ReportPlayM4Error);
        Assert.False(info.AttributePlayM4Error32AsRootCause);
    }

    [Fact]
    public void TimeoutDiagnostics_InitializedPort_ReportsRealPlayM4Error()
    {
        var info = StreamCaptureTimeoutDiagnostics.Classify(
            decoderPort: 3,
            isInitialized: true,
            playM4ErrorFromValidPort: 32);

        Assert.Equal(StreamCaptureTimeoutDiagnostics.ReasonOpenStreamOrPlayFailed, info.Reason);
        Assert.Equal(32, info.ReportPlayM4Error);
        Assert.True(info.AttributePlayM4Error32AsRootCause);
    }

    [Fact]
    public void SoftResetPolicy_AllowsFirstAttempt_AndSkipsWithinCooldown()
    {
        var now = DateTimeOffset.Parse("2026-09-11T02:00:00Z");
        Assert.True(SdkSoftResetPolicy.CanAttempt(now, lastSucceededAt: null));

        var last = now.AddSeconds(-10);
        Assert.False(SdkSoftResetPolicy.CanAttempt(now, last));

        Assert.True(SdkSoftResetPolicy.CanAttempt(now.AddSeconds(30), last));
    }

    [Fact]
    public void SystemSettings_StreamCaptureDecoderTimeoutMs_DefaultsFromSingleSource()
    {
        var settings = new SystemSettings();
        Assert.Equal(SystemSettings.DefaultStreamCaptureDecoderTimeoutMs, settings.StreamCaptureDecoderTimeoutMs);
        Assert.Equal(SystemSettings.DefaultStreamCaptureDecoderTimeoutMs, settings.ResolveStreamCaptureDecoderTimeoutMs());

        settings.StreamCaptureDecoderTimeoutMs = 0;
        Assert.Equal(SystemSettings.DefaultStreamCaptureDecoderTimeoutMs, settings.ResolveStreamCaptureDecoderTimeoutMs());

        settings.StreamCaptureDecoderTimeoutMs = 8000;
        Assert.Equal(8000, settings.ResolveStreamCaptureDecoderTimeoutMs());
    }

    [Fact]
    public async Task TrySoftResetSdkAsync_SkipsCleanup_WhenWithinCooldown()
    {
        var service = new HikvisionService();
        service.LastSoftResetSucceededAt = DateTimeOffset.UtcNow;

        var result = await service.TrySoftResetSdkAsync();

        Assert.False(result.Performed);
        Assert.True(result.SkippedDueToCooldown);
        Assert.Contains("30s", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SdkSoftResetResult_Factories_AreConsistent()
    {
        var ok = SdkSoftResetResult.Succeeded(123);
        Assert.True(ok.Performed);
        Assert.False(ok.SkippedDueToCooldown);
        Assert.Equal(123, ok.DurationMs);

        var cool = SdkSoftResetResult.CooldownSkipped("wait");
        Assert.False(cool.Performed);
        Assert.True(cool.SkippedDueToCooldown);
    }
}
