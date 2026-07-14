namespace MaterialClient.Common.Configuration;

/// <summary>
///     Local camera / Lpr image retention cleanup options.
///     Bound from <c>BackgroundServices:ImageCleanup</c>.
/// </summary>
public class ImageCleanupOptions
{
    public const string SectionName = "BackgroundServices:ImageCleanup";

    /// <summary>
    ///     Whether to register and run the cleanup background worker. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Retention in days; files older than today minus this value may be deleted. Default: 90.
    /// </summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    ///     Cleanup interval in hours after each successful run cycle. Default: 24.
    /// </summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>
    ///     Hours to wait after the worker's first tick before running cleanup.
    ///     Not a clock hour — relative delay from process start. Default: 1. Use 0 to run immediately.
    /// </summary>
    public int InitialDelayHours { get; set; } = 1;
}
