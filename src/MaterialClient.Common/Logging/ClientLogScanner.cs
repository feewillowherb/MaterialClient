using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MaterialClient.Common.Logging;

/// <summary>
/// Scans client log files from standardized and legacy directory layouts.
/// </summary>
public static class ClientLogScanner
{
    public static IReadOnlyList<ClientLogFileEntry> Scan(string logBaseDirectory, string? dateFolder)
    {
        if (!Directory.Exists(logBaseDirectory))
        {
            return Array.Empty<ClientLogFileEntry>();
        }

        var discovered = new Dictionary<string, ClientLogFileEntry>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(dateFolder))
        {
            foreach (var file in Directory.EnumerateFiles(logBaseDirectory, "*.log", SearchOption.AllDirectories))
            {
                AddFile(logBaseDirectory, file, discovered);
            }

            return discovered.Values
                .OrderBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var normalizedDateFolder = dateFolder.Replace('\\', '/').Trim('/');
        TryGetCompactDate(normalizedDateFolder, out var compactDate);

        var candidateDirectories = new List<string>
        {
            Path.Combine(logBaseDirectory, normalizedDateFolder)
        };

        var legacyLiteralDirectory = Path.Combine(logBaseDirectory, "{YYYY}", "{MM}", "{DD}");
        if (Directory.Exists(legacyLiteralDirectory))
        {
            candidateDirectories.Add(legacyLiteralDirectory);
        }

        foreach (var directory in candidateDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            var isLegacyLiteralDirectory = string.Equals(
                directory,
                legacyLiteralDirectory,
                StringComparison.OrdinalIgnoreCase);

            foreach (var file in Directory.EnumerateFiles(directory, "*.log", SearchOption.TopDirectoryOnly))
            {
                if (isLegacyLiteralDirectory &&
                    !string.IsNullOrEmpty(compactDate) &&
                    !Path.GetFileName(file).Contains(compactDate, StringComparison.Ordinal))
                {
                    continue;
                }

                AddFile(logBaseDirectory, file, discovered);
            }
        }

        if (!string.IsNullOrEmpty(compactDate))
        {
            foreach (var file in Directory.EnumerateFiles(logBaseDirectory, "*.log", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.Contains(compactDate, StringComparison.Ordinal))
                {
                    AddFile(logBaseDirectory, file, discovered);
                }
            }
        }

        return discovered.Values
            .OrderBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ToRelativeDirectory(string logBaseDirectory, string fileFullPath)
    {
        var directory = Path.GetDirectoryName(fileFullPath) ?? logBaseDirectory;
        var relative = Path.GetRelativePath(logBaseDirectory, directory);
        if (relative is "." or "")
        {
            return string.Empty;
        }

        return relative.Replace('\\', '/') + "/";
    }

    private static void AddFile(
        string logBaseDirectory,
        string fileFullPath,
        IDictionary<string, ClientLogFileEntry> discovered)
    {
        var fileInfo = new FileInfo(fileFullPath);
        var entry = new ClientLogFileEntry(
            fileInfo.Name,
            ToRelativeDirectory(logBaseDirectory, fileFullPath),
            fileInfo.Length,
            fileInfo.LastWriteTimeUtc);

        discovered[fileFullPath] = entry;
    }

    private static bool TryGetCompactDate(string dateFolder, out string compactDate)
    {
        compactDate = string.Empty;
        var segments = dateFolder.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(segments[0], out var year) ||
            !int.TryParse(segments[1], out var month) ||
            !int.TryParse(segments[2], out var day))
        {
            return false;
        }

        try
        {
            var date = new DateTime(year, month, day);
            compactDate = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}

public record ClientLogFileEntry(
    string FileName,
    string FilePath,
    long FileSize,
    DateTime LastModifiedUtc);
