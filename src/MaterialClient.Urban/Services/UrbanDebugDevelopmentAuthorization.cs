#if DEBUG
namespace MaterialClient.Urban.Services;

/// <summary>
///     Canonical Debug-only Urban development authorization fields.
///     Pure data — not registered in DI. Machine code comes from <c>IMachineCodeService</c>.
/// </summary>
public static class UrbanDebugDevelopmentAuthorization
{
    /// <summary>
    ///     Demo ProjectId aligned with pipelines/_shared/urban/seeds/demo-license.json.
    /// </summary>
    public static readonly Guid ProjectId = Guid.Parse("08DDCF46-3744-D3E1-1999-0D645800B322");

    public const string AccessCode = "XNXS20260611001";

    /// <summary>
    ///     Display name for the demo project (ASCII to satisfy code character rules).
    /// </summary>
    public const string ProName = "Hangzhou FanDong Demo Project";

    public static readonly DateTime AuthEndTime = new(2029, 10, 21, 16, 0, 0, DateTimeKind.Local);

    public const string SuccessMessage = "DEBUG development authorization context active";
}
#endif
