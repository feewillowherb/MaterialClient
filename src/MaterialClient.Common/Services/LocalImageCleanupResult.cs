namespace MaterialClient.Common.Services;

/// <summary>
///     Result of one local image cleanup pass.
/// </summary>
public sealed record LocalImageCleanupResult(int DeletedFiles, int FailedDeletes, int SkippedRoots);
