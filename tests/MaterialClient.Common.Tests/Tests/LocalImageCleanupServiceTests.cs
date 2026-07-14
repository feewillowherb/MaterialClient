using System;
using System.IO;
using System.Threading.Tasks;
using MaterialClient.Common.Backgrounds;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class LocalImageCleanupServiceTests
{
    [Fact]
    public async Task CleanupAsync_DeletesExpiredDatedAndFlatFiles_KeepsRecent()
    {
        var root = CreateTempRoot();
        try
        {
            var localNow = new DateTime(2026, 7, 14, 12, 0, 0);
            var options = new ImageCleanupOptions { RetentionDays = 90 };

            var expiredJianKong = Path.Combine(root, "PhotoJianKong", "2026", "01", "01", "cam.jpg");
            var expiredLprDated = Path.Combine(root, "Lpr", "2026", "01", "01", "plate.jpg");
            var expiredUrbanLegacy = Path.Combine(root, "PhotoUrban", "2026", "01", "01", "urban.jpg");
            var recentLpr = Path.Combine(root, "Lpr", "2026", "07", "01", "recent.jpg");
            var flatOld = Path.Combine(root, "Lpr", "flat_old.jpg");
            var flatNew = Path.Combine(root, "Lpr", "flat_new.jpg");

            WriteFile(expiredJianKong);
            WriteFile(expiredLprDated);
            WriteFile(expiredUrbanLegacy);
            WriteFile(recentLpr);
            WriteFile(flatOld);
            WriteFile(flatNew);

            File.SetLastWriteTime(flatOld, localNow.AddDays(-100));
            File.SetLastWriteTime(flatNew, localNow.AddDays(-10));

            var service = CreateService();
            var result = await service.CleanupAsync(root, options, localNow);

            result.DeletedFiles.ShouldBe(4); // jiankong + lpr dated + photoUrban + flat_old
            File.Exists(expiredJianKong).ShouldBeFalse();
            File.Exists(expiredLprDated).ShouldBeFalse();
            File.Exists(expiredUrbanLegacy).ShouldBeFalse();
            File.Exists(flatOld).ShouldBeFalse();
            File.Exists(recentLpr).ShouldBeTrue();
            File.Exists(flatNew).ShouldBeTrue();
        }
        finally
        {
            TryDeleteTree(root);
        }
    }

    [Fact]
    public async Task CleanupAsync_WhenRetentionDaysInvalid_SkipsDeletion()
    {
        var root = CreateTempRoot();
        try
        {
            var file = Path.Combine(root, "Lpr", "2020", "01", "01", "old.jpg");
            WriteFile(file);

            var service = CreateService();
            var result = await service.CleanupAsync(
                root,
                new ImageCleanupOptions { RetentionDays = 0 },
                new DateTime(2026, 7, 14));

            result.DeletedFiles.ShouldBe(0);
            File.Exists(file).ShouldBeTrue();
        }
        finally
        {
            TryDeleteTree(root);
        }
    }

    [Fact]
    public void ComputeInitialDelay_WhenZeroOrNegative_ReturnsZero()
    {
        ImageCleanupBackgroundService.ComputeInitialDelay(0).ShouldBe(TimeSpan.Zero);
        ImageCleanupBackgroundService.ComputeInitialDelay(-1).ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void ComputeInitialDelay_WhenPositive_ReturnsHours()
    {
        ImageCleanupBackgroundService.ComputeInitialDelay(1).ShouldBe(TimeSpan.FromHours(1));
        ImageCleanupBackgroundService.ComputeInitialDelay(3).ShouldBe(TimeSpan.FromHours(3));
    }

    private static LocalImageCleanupService CreateService()
        => new(Options.Create(new ImageCleanupOptions()), NullLogger<LocalImageCleanupService>.Instance);

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mc-image-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [0xFF, 0xD8, 0xFF, 0xD9]);
    }

    private static void TryDeleteTree(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // ignore
        }
    }
}
