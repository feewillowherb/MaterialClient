using System;
using System.IO;
using MaterialClient.Common.Utils;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class AttachmentPathUtilsTests
{
    [Fact]
    public void ToAbsolutePath_WhenRelative_ReturnsAppBaseDirectoryCombinedPath()
    {
        // Arrange
        var relative = "PhotoPiaoJu/2026/01/23/bill_test.jpg";
        var expected = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relative));

        // Act
        var actual = AttachmentPathUtils.ToAbsolutePath(relative);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToAbsolutePath_WhenAbsolute_ReturnsSamePath()
    {
        // Arrange
        var absolute = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "PhotoPiaoJu", "abs_test.jpg"));

        // Act
        var actual = AttachmentPathUtils.ToAbsolutePath(absolute);

        // Assert
        Assert.Equal(absolute, actual);
    }

    [Fact]
    public void ToAbsolutePath_WhenNullOrWhiteSpace_ReturnsEmptyString()
    {
        // Act
        var actual1 = AttachmentPathUtils.ToAbsolutePath(null);
        var actual2 = AttachmentPathUtils.ToAbsolutePath("  ");

        // Assert
        Assert.Equal(string.Empty, actual1);
        Assert.Equal(string.Empty, actual2);
    }

    [Fact]
    public void FileExists_WhenCurrentDirectoryChanges_ReturnsCorrectResultBasedOnAppBaseDirectory()
    {
        // Arrange
        var baseDir = AppContext.BaseDirectory;
        var dirName = "tests_attachmentpathutils";
        var relativeDir = Path.Combine(dirName, "sub");
        var relativeFile = Path.Combine(relativeDir, "exists_test.txt");
        var absoluteFile = Path.Combine(baseDir, relativeFile);

        Directory.CreateDirectory(Path.GetDirectoryName(absoluteFile)!);
        File.WriteAllText(absoluteFile, "ok");

        var originalCwd = Directory.GetCurrentDirectory();
        try
        {
            // Act: simulate a bad working directory (e.g., System32 on auto-start)
            Directory.SetCurrentDirectory(Path.GetTempPath());

            var exists = AttachmentPathUtils.FileExists(relativeFile);

            // Assert
            Assert.True(exists);
        }
        finally
        {
            // Cleanup
            try
            {
                if (File.Exists(absoluteFile)) File.Delete(absoluteFile);
                var d = Path.GetDirectoryName(absoluteFile);
                if (!string.IsNullOrWhiteSpace(d) && Directory.Exists(d)) Directory.Delete(d, recursive: true);
            }
            catch
            {
                // ignore cleanup failures
            }

            Directory.SetCurrentDirectory(originalCwd);
        }
    }
}

