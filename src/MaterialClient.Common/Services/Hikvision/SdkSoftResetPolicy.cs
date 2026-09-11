namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     Pure cooldown policy for HCNetSDK soft reset (default 30 seconds).
/// </summary>
public static class SdkSoftResetPolicy
{
    public static readonly TimeSpan DefaultCooldown = TimeSpan.FromSeconds(30);

    public static bool CanAttempt(DateTimeOffset now, DateTimeOffset? lastSucceededAt, TimeSpan? cooldown = null)
    {
        var window = cooldown ?? DefaultCooldown;
        if (lastSucceededAt is null)
            return true;

        return now - lastSucceededAt.Value >= window;
    }
}
