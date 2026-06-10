namespace MaterialClient.Common.Utils;

/// <summary>
/// Path management utility for bidirectional path conversion.
/// 
/// Strategy:
/// - Storage (database/config): Relative paths for portability
/// - Runtime (file I/O): Absolute paths based on AppContext.BaseDirectory
/// 
/// This ensures:
/// 1. Database can be migrated between servers
/// 2. App works when launched from any working directory (e.g., System32)
/// 3. All file operations use consistent absolute paths
/// </summary>
public static class PathManager
{
    private static readonly string _appBaseDirectory = AppContext.BaseDirectory;

    /// <summary>
    /// Convert any path to absolute path for file system operations.
    /// Idempotent: calling multiple times returns same result.
    /// </summary>
    /// <param name="path">Relative or absolute path</param>
    /// <returns>Absolute path based on application base directory</returns>
    /// <example>
    /// ToAbsolutePath("Photos/car.jpg") → "D:\MaterialClient\Photos\car.jpg"
    /// ToAbsolutePath("D:\MaterialClient\Photos\car.jpg") → "D:\MaterialClient\Photos\car.jpg" (unchanged)
    /// </example>
    public static string ToAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return _appBaseDirectory;

        // Already absolute path, return unchanged
        if (Path.IsPathRooted(path))
            return path;

        // Convert relative path to absolute based on app directory
        return Path.GetFullPath(Path.Combine(_appBaseDirectory, path));
    }

    /// <summary>
    /// Convert absolute path to relative path for database storage.
    /// If path is outside app directory, returns absolute path unchanged.
    /// Idempotent: calling multiple times returns same result.
    /// </summary>
    /// <param name="absolutePath">Absolute path to convert</param>
    /// <returns>Relative path if inside app directory, otherwise absolute path</returns>
    /// <example>
    /// ToRelativePath("D:\MaterialClient\Photos\car.jpg") → "Photos\car.jpg"
    /// ToRelativePath("C:\Users\Admin\Desktop\export.pdf") → "C:\Users\Admin\Desktop\export.pdf" (outside app dir)
    /// ToRelativePath("Photos/car.jpg") → "Photos/car.jpg" (already relative)
    /// </example>
    public static string ToRelativePath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return string.Empty;

        // If not rooted (already relative), return unchanged
        if (!Path.IsPathRooted(absolutePath))
            return absolutePath;

        // If path is not under app directory, keep it absolute
        if (!absolutePath.StartsWith(_appBaseDirectory, StringComparison.OrdinalIgnoreCase))
            return absolutePath;

        // Convert to relative path
        return Path.GetRelativePath(_appBaseDirectory, absolutePath);
    }

    /// <summary>
    /// Check if file exists with automatic path normalization.
    /// Converts relative paths to absolute before checking existence.
    /// </summary>
    /// <param name="path">Relative or absolute path</param>
    /// <returns>True if file exists, false otherwise</returns>
    /// <example>
    /// FileExists("Photos/car.jpg") → checks D:\MaterialClient\Photos\car.jpg
    /// </example>
    public static bool FileExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var absolutePath = ToAbsolutePath(path);
        return File.Exists(absolutePath);
    }

    /// <summary>
    /// Create directory with automatic path normalization.
    /// Converts relative paths to absolute before creating directory.
    /// Creates all missing parent directories.
    /// </summary>
    /// <param name="path">Relative or absolute path</param>
    /// <returns>Absolute path of created directory</returns>
    /// <example>
    /// EnsureDirectoryExists("Photos/2026/01/23") → creates D:\MaterialClient\Photos\2026\01\23 and returns absolute path
    /// </example>
    public static string EnsureDirectoryExists(string path)
    {
        var absolutePath = ToAbsolutePath(path);
        Directory.CreateDirectory(absolutePath);
        return absolutePath;
    }
}
