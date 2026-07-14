using MaterialClient.Common.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     Deletes expired local camera / Lpr image files under application directories.
/// </summary>
public interface ILocalImageCleanupService
{
    /// <summary>
    ///     Run cleanup under <see cref="AppContext.BaseDirectory"/> using configured options.
    /// </summary>
    Task<LocalImageCleanupResult> CleanupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Run cleanup under an explicit root (for tests). Uses <paramref name="options"/> and <paramref name="localNow"/>.
    /// </summary>
    Task<LocalImageCleanupResult> CleanupAsync(
        string rootDirectory,
        ImageCleanupOptions options,
        DateTime localNow,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ILocalImageCleanupService" />
public class LocalImageCleanupService : ILocalImageCleanupService, ITransientDependency
{
    private static readonly string[] CleanupRoots = ["PhotoJianKong", "Lpr", "PhotoUrban"];

    private readonly IOptions<ImageCleanupOptions> _options;
    private readonly ILogger<LocalImageCleanupService>? _logger;

    public LocalImageCleanupService(
        IOptions<ImageCleanupOptions> options,
        ILogger<LocalImageCleanupService>? logger = null)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<LocalImageCleanupResult> CleanupAsync(CancellationToken cancellationToken = default)
        => CleanupAsync(AppContext.BaseDirectory, _options.Value, DateTime.Now, cancellationToken);

    /// <inheritdoc />
    public Task<LocalImageCleanupResult> CleanupAsync(
        string rootDirectory,
        ImageCleanupOptions options,
        DateTime localNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(options);

        if (options.RetentionDays < 1)
        {
            _logger?.LogWarning(
                "Image cleanup skipped: RetentionDays={RetentionDays} must be >= 1",
                options.RetentionDays);
            return Task.FromResult(new LocalImageCleanupResult(0, 0, 0));
        }

        var cutoffDate = localNow.Date.AddDays(-options.RetentionDays);
        var deleted = 0;
        var failed = 0;
        var skippedRoots = 0;

        foreach (var rootName in CleanupRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rootPath = Path.Combine(rootDirectory, rootName);
            if (!Directory.Exists(rootPath))
            {
                skippedRoots++;
                continue;
            }

            var rootResult = CleanupRoot(rootPath, cutoffDate, cancellationToken);
            deleted += rootResult.DeletedFiles;
            failed += rootResult.FailedDeletes;
        }

        _logger?.LogInformation(
            "Image cleanup finished: Deleted={Deleted}, Failed={Failed}, CutoffDate={CutoffDate:yyyy-MM-dd}",
            deleted, failed, cutoffDate);

        return Task.FromResult(new LocalImageCleanupResult(deleted, failed, skippedRoots));
    }

    private LocalImageCleanupResult CleanupRoot(
        string rootPath,
        DateTime cutoffDate,
        CancellationToken cancellationToken)
    {
        var deleted = 0;
        var failed = 0;

        // Flat files directly under root (legacy Lpr/*.jpg)
        foreach (var file in Directory.EnumerateFiles(rootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var writeTime = File.GetLastWriteTime(file).Date;
                if (writeTime < cutoffDate)
                {
                    File.Delete(file);
                    deleted++;
                }
            }
            catch (Exception ex)
            {
                failed++;
                _logger?.LogWarning(ex, "Failed to delete image file: {Path}", file);
            }
        }

        // Dated layout: {root}/{yyyy}/{MM}/{dd}/
        foreach (var yearDir in SafeEnumerateDirectories(rootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var yearName = Path.GetFileName(yearDir);
            if (!int.TryParse(yearName, out var year) || year < 2000 || year > 2100)
                continue;

            foreach (var monthDir in SafeEnumerateDirectories(yearDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var monthName = Path.GetFileName(monthDir);
                if (!int.TryParse(monthName, out var month) || month is < 1 or > 12)
                    continue;

                foreach (var dayDir in SafeEnumerateDirectories(monthDir))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var dayName = Path.GetFileName(dayDir);
                    if (!int.TryParse(dayName, out var day) || day is < 1 or > 31)
                        continue;

                    DateTime folderDate;
                    try
                    {
                        folderDate = new DateTime(year, month, day);
                    }
                    catch
                    {
                        continue;
                    }

                    if (folderDate >= cutoffDate)
                        continue;

                    foreach (var file in Directory.EnumerateFiles(dayDir))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            File.Delete(file);
                            deleted++;
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            _logger?.LogWarning(ex, "Failed to delete image file: {Path}", file);
                        }
                    }

                    TryDeleteEmptyDirectory(dayDir);
                }

                TryDeleteEmptyDirectory(monthDir);
            }

            TryDeleteEmptyDirectory(yearDir);
        }

        return new LocalImageCleanupResult(deleted, failed, 0);
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path);
        }
        catch
        {
            return [];
        }
    }

    private void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                Directory.Delete(path);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Could not remove empty directory: {Path}", path);
        }
    }
}
