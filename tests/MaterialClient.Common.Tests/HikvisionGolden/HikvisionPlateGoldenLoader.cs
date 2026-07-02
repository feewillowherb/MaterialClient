using System.Runtime.InteropServices;
using System.Text.Json;
using MaterialClient.Common.Services.Hikvision;

namespace MaterialClient.Common.Tests.HikvisionGolden;

/// <summary>
///     加载 Golden binary 并修补图片指针，供 InvokePlateAlarmCallbackForTests 使用。
/// </summary>
public sealed class HikvisionPlateGoldenLoader : IDisposable
{
    private readonly List<IntPtr> _allocatedPointers = [];

    public static string ResolveGoldenRootDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "TestData", "HikvisionGolden"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestData", "HikvisionGolden"))
        };

        foreach (var path in candidates)
        {
            var manifestPath = Path.Combine(path, HikvisionPlateGoldenManifest.ManifestFileName);
            if (File.Exists(manifestPath))
            {
                return path;
            }
        }

        throw new DirectoryNotFoundException(
            "未找到 TestData/HikvisionGolden/manifest.json，请先运行 RegenerateHikvisionPlateGoldenFixtures 生成夹具。");
    }

    public static HikvisionPlateGoldenManifest LoadManifest(string? goldenRootDirectory = null)
    {
        goldenRootDirectory ??= ResolveGoldenRootDirectory();
        var json = File.ReadAllText(Path.Combine(goldenRootDirectory, HikvisionPlateGoldenManifest.ManifestFileName));
        return JsonSerializer.Deserialize<HikvisionPlateGoldenManifest>(json)
               ?? throw new InvalidOperationException("manifest.json 解析失败");
    }

    public HikvisionPlateGoldenReplay PrepareReplay(HikvisionPlateGoldenFixtureEntry entry, string goldenRootDirectory)
    {
        var fixtureDir = Path.Combine(goldenRootDirectory, entry.Folder);
        var alarmerBytes = File.ReadAllBytes(Path.Combine(fixtureDir, entry.AlarmerFile));
        var alarmInfoBytes = File.ReadAllBytes(Path.Combine(fixtureDir, entry.AlarmInfoFile));

        if (alarmerBytes.Length != Marshal.SizeOf<HikvisionSdk.NET_DVR_ALARMER>())
        {
            throw new InvalidDataException(
                $"alarmer.bin 大小应为 {Marshal.SizeOf<HikvisionSdk.NET_DVR_ALARMER>()}，实际 {alarmerBytes.Length}");
        }

        var pAlarmer = AllocAndCopy(alarmerBytes);
        var pAlarmInfo = AllocAndCopy(alarmInfoBytes);
        var dwBufLen = (uint)alarmInfoBytes.Length;

        if (!string.IsNullOrEmpty(entry.ImageFile) && entry.ImageBinding is not null)
        {
            var imageBytes = File.ReadAllBytes(Path.Combine(fixtureDir, entry.ImageFile));
            PatchImagePointers(entry, pAlarmInfo, imageBytes);
        }

        return new HikvisionPlateGoldenReplay(entry.LCommand, pAlarmer, pAlarmInfo, dwBufLen);
    }

    private void PatchImagePointers(HikvisionPlateGoldenFixtureEntry entry, IntPtr pAlarmInfo, byte[] imageBytes)
    {
        var pImage = AllocAndCopy(imageBytes);
        var binding = entry.ImageBinding!;

        if (string.Equals(binding.Type, "upload", StringComparison.OrdinalIgnoreCase))
        {
            var plateResult = Marshal.PtrToStructure<HikvisionSdk.NET_DVR_PLATE_RESULT>(pAlarmInfo);
            plateResult.dwPicLen = (uint)imageBytes.Length;
            plateResult.pBuffer1 = pImage;
            Marshal.StructureToPtr(plateResult, pAlarmInfo, false);
            return;
        }

        if (string.Equals(binding.Type, "its", StringComparison.OrdinalIgnoreCase))
        {
            var itsResult = Marshal.PtrToStructure<HikvisionSdk.NET_ITS_PLATE_RESULT>(pAlarmInfo);
            itsResult.dwPicNum = 1;
            var index = Math.Clamp(binding.PicIndex, 0, itsResult.struPicInfo.Length - 1);
            var picInfo = itsResult.struPicInfo[index];
            picInfo.byType = HikvisionSdk.HikItsPictureTypeScene;
            picInfo.dwDataLen = (uint)imageBytes.Length;
            picInfo.pBuffer = pImage;
            itsResult.struPicInfo[index] = picInfo;
            Marshal.StructureToPtr(itsResult, pAlarmInfo, false);
            return;
        }

        throw new NotSupportedException($"不支持的 imageBinding.type: {binding.Type}");
    }

    private IntPtr AllocAndCopy(byte[] bytes)
    {
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        _allocatedPointers.Add(ptr);
        return ptr;
    }

    public void Dispose()
    {
        foreach (var ptr in _allocatedPointers)
        {
            Marshal.FreeHGlobal(ptr);
        }

        _allocatedPointers.Clear();
    }
}

public sealed record HikvisionPlateGoldenReplay(int LCommand, IntPtr PAlarmer, IntPtr PAlarmInfo, uint DwBufLen);
