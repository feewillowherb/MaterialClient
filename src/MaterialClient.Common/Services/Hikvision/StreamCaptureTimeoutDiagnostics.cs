namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     Classifies PlayM4 WaitForPlaying timeout causes so missing SYSHEAD is not blamed on PlayM4 error 32.
/// </summary>
public static class StreamCaptureTimeoutDiagnostics
{
    public const string ReasonNoSysHead = "no_syshead";
    public const string ReasonOpenStreamOrPlayFailed = "openstream_or_play_failed";

    public static StreamCaptureTimeoutInfo Classify(int decoderPort, bool isInitialized, int playM4ErrorFromValidPort)
    {
        if (decoderPort < 0 || !isInitialized)
        {
            return new StreamCaptureTimeoutInfo(
                Reason: ReasonNoSysHead,
                ReportPlayM4Error: 0,
                AttributePlayM4Error32AsRootCause: false);
        }

        return new StreamCaptureTimeoutInfo(
            Reason: ReasonOpenStreamOrPlayFailed,
            ReportPlayM4Error: playM4ErrorFromValidPort,
            AttributePlayM4Error32AsRootCause: playM4ErrorFromValidPort == 32);
    }
}

/// <summary>
///     Structured WaitForPlaying timeout diagnosis.
/// </summary>
public sealed record StreamCaptureTimeoutInfo(
    string Reason,
    int ReportPlayM4Error,
    bool AttributePlayM4Error32AsRootCause);
