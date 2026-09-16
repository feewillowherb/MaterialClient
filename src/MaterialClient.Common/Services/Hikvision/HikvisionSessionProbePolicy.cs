namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     Throttle policy for CHECK_USER_STATUS probes (default 30 seconds).
/// </summary>
public static class HikvisionSessionProbePolicy
{
    public static readonly TimeSpan DefaultThrottle = TimeSpan.FromSeconds(30);

    public static bool CanProbe(DateTimeOffset now, DateTimeOffset? lastSucceededAt, TimeSpan? throttle = null)
    {
        var window = throttle ?? DefaultThrottle;
        if (lastSucceededAt is null)
            return true;

        return now - lastSucceededAt.Value >= window;
    }
}
