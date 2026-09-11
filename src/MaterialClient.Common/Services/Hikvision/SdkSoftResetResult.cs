namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     Outcome of an HCNetSDK soft-reset attempt (Cleanup + Init).
/// </summary>
public sealed record SdkSoftResetResult(
    bool Performed,
    bool SkippedDueToCooldown,
    long DurationMs,
    string? Message)
{
    public static SdkSoftResetResult Succeeded(long durationMs) =>
        new(Performed: true, SkippedDueToCooldown: false, DurationMs: durationMs, Message: null);

    public static SdkSoftResetResult CooldownSkipped(string message) =>
        new(Performed: false, SkippedDueToCooldown: true, DurationMs: 0, Message: message);

    public static SdkSoftResetResult Failed(string message, long durationMs = 0) =>
        new(Performed: false, SkippedDueToCooldown: false, DurationMs: durationMs, Message: message);
}
