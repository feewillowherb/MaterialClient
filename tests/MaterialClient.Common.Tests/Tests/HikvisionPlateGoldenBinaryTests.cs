using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Tests.HikvisionGolden;
using MaterialClient.Common.Tests.Tests;
using MaterialClient.Common.Utils;
using NSubstitute;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     使用 Golden binary 回放测试 HandlePlateResult / HandleItsPlateResult（无物理设备）。
/// </summary>
public class HikvisionPlateGoldenBinaryTests
{
    private readonly ITestOutputHelper _output;

    public HikvisionPlateGoldenBinaryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    ///     手动执行以重新生成 TestData/HikvisionGolden 下的 .bin / manifest（合成数据，后续可替换为真机 dump）。
    /// </summary>
    [Fact(Skip = "手动执行：dotnet test --filter FullyQualifiedName~RegenerateHikvisionPlateGoldenFixtures")]
    public void RegenerateHikvisionPlateGoldenFixtures()
    {
        var goldenRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "TestData", "HikvisionGolden"));
        HikvisionPlateGoldenFixtureBuilder.WriteAllFixtures(goldenRoot);
        _output.WriteLine($"Golden fixtures written to: {goldenRoot}");
    }

    public static IEnumerable<object[]> GoldenFixtureIds()
    {
        var goldenRoot = HikvisionPlateGoldenLoader.ResolveGoldenRootDirectory();
        var manifest = HikvisionPlateGoldenLoader.LoadManifest(goldenRoot);
        return manifest.Fixtures.Select(f => new object[] { f.Id });
    }

    [Theory]
    [MemberData(nameof(GoldenFixtureIds))]
    public void PlateAlarmGoldenBinary_ParsesAndPublishesEvent(string fixtureId)
    {
        var goldenRoot = HikvisionPlateGoldenLoader.ResolveGoldenRootDirectory();
        var manifest = HikvisionPlateGoldenLoader.LoadManifest(goldenRoot);
        var entry = manifest.Fixtures.Single(f => f.Id == fixtureId);

        var eventBus = new TestLocalEventBus();
        LicensePlateRecognizedEventData? captured = null;
        eventBus.Subscribe<LicensePlateRecognizedEventData>(e => captured = e);

        var settingsService = Substitute.For<ISettingsService>();
        settingsService.GetSettingsAsync().Returns(Task.FromResult(new SettingsEntity(
            new ScaleSettings(),
            new DocumentScannerConfig(),
            new SystemSettings { DefaultWeighingMode = WeighingMode.UrbanMode },
            [],
            [],
            new WeighingConfiguration(),
            new SoundDeviceSettings())));

        var service = new HikvisionLprService(settingsService, eventBus);
        service.AddOrUpdateDevice(new LicensePlateRecognitionConfig
        {
            Ip = entry.DeviceIp,
            Name = entry.DeviceName,
            Direction = LicensePlateDirection.A,
            UserName = "admin",
            Password = "admin123",
            Port = "8000",
            Channel = "1"
        });

        using var loader = new HikvisionPlateGoldenLoader();
        var replay = loader.PrepareReplay(entry, goldenRoot);

        service.InvokePlateAlarmCallbackForTests(
            replay.LCommand,
            replay.PAlarmer,
            replay.PAlarmInfo,
            replay.DwBufLen,
            WeighingMode.UrbanMode);

        Assert.NotNull(captured);
        Assert.Equal(entry.ExpectedPlate, captured!.PlateNumber);
        Assert.Equal(entry.DeviceName, captured.DeviceName);
        Assert.Equal(LprDeviceType.Hikvision, captured.DeviceType);

        if (!string.IsNullOrEmpty(entry.ExpectedPlateColor))
        {
            Assert.Equal(entry.ExpectedPlateColor, captured.PlateColor);
        }

        if (!string.IsNullOrEmpty(entry.ExpectedVehicleColor))
        {
            Assert.Equal(entry.ExpectedVehicleColor, captured.VehicleColor);
        }

        if (!string.IsNullOrEmpty(entry.ExpectedVehicleType))
        {
            Assert.Equal(entry.ExpectedVehicleType, captured.VehicleType);
        }

        if (!string.IsNullOrEmpty(entry.ImageFile))
        {
            Assert.False(string.IsNullOrWhiteSpace(captured.LprImagePath));
            Assert.True(PathManager.FileExists(captured.LprImagePath!),
                $"Lpr 图片应已保存: {captured.LprImagePath}");
            _output.WriteLine($"Lpr image saved: {captured.LprImagePath}");
        }

        _output.WriteLine($"Fixture {fixtureId}: plate={captured.PlateNumber}, command=0x{entry.LCommand:X}");
    }

    [Fact]
    public void PlateAlarm_InvalidPlate_StillSavesLprAndPublishesEmptyPlate()
    {
        CodePagesEncodingInitializer.Register();

        var goldenRoot = HikvisionPlateGoldenLoader.ResolveGoldenRootDirectory();
        var manifest = HikvisionPlateGoldenLoader.LoadManifest(goldenRoot);
        var entry = manifest.Fixtures.Single(f => f.Id == "upload_yellow_plate");

        var eventBus = new TestLocalEventBus();
        LicensePlateRecognizedEventData? captured = null;
        eventBus.Subscribe<LicensePlateRecognizedEventData>(e => captured = e);

        var settingsService = Substitute.For<ISettingsService>();
        settingsService.GetSettingsAsync().Returns(Task.FromResult(new SettingsEntity(
            new ScaleSettings(),
            new DocumentScannerConfig(),
            new SystemSettings { DefaultWeighingMode = WeighingMode.UrbanMode },
            [],
            [],
            new WeighingConfiguration(),
            new SoundDeviceSettings())));

        var service = new HikvisionLprService(settingsService, eventBus);
        service.AddOrUpdateDevice(new LicensePlateRecognitionConfig
        {
            Ip = entry.DeviceIp,
            Name = entry.DeviceName,
            Direction = LicensePlateDirection.A,
            UserName = "admin",
            Password = "admin123",
            Port = "8000",
            Channel = "1"
        });

        using var loader = new HikvisionPlateGoldenLoader();
        var replay = loader.PrepareReplay(entry, goldenRoot);

        var plateResult = Marshal.PtrToStructure<HikvisionSdk.NET_DVR_PLATE_RESULT>(replay.PAlarmInfo);
        plateResult.struPlateInfo.sLicense = EncodeGbkLicense("车牌");
        Marshal.StructureToPtr(plateResult, replay.PAlarmInfo, false);

        service.InvokePlateAlarmCallbackForTests(
            replay.LCommand,
            replay.PAlarmer,
            replay.PAlarmInfo,
            replay.DwBufLen,
            WeighingMode.UrbanMode);

        Assert.NotNull(captured);
        Assert.Equal(string.Empty, captured!.PlateNumber);
        Assert.False(string.IsNullOrWhiteSpace(captured.LprImagePath));
        Assert.True(PathManager.FileExists(captured.LprImagePath!),
            $"无效车牌仍应保存 Lpr 图片: {captured.LprImagePath}");
    }

    [Fact]
    public void PlateAlarm_StandardModeWithCameraConfigs_StillSavesLpr()
    {
        CodePagesEncodingInitializer.Register();

        var goldenRoot = HikvisionPlateGoldenLoader.ResolveGoldenRootDirectory();
        var manifest = HikvisionPlateGoldenLoader.LoadManifest(goldenRoot);
        var entry = manifest.Fixtures.First(f => !string.IsNullOrEmpty(f.ImageFile));

        var eventBus = new TestLocalEventBus();
        LicensePlateRecognizedEventData? captured = null;
        eventBus.Subscribe<LicensePlateRecognizedEventData>(e => captured = e);

        var settingsService = Substitute.For<ISettingsService>();
        settingsService.GetSettingsAsync().Returns(Task.FromResult(new SettingsEntity(
            new ScaleSettings(),
            new DocumentScannerConfig(),
            new SystemSettings { DefaultWeighingMode = WeighingMode.Standard },
            new List<CameraConfig> { new() { Name = "cam1", Ip = "192.168.1.10" } },
            [],
            new WeighingConfiguration(),
            new SoundDeviceSettings())));

        var service = new HikvisionLprService(settingsService, eventBus);
        service.AddOrUpdateDevice(new LicensePlateRecognitionConfig
        {
            Ip = entry.DeviceIp,
            Name = entry.DeviceName,
            Direction = LicensePlateDirection.A,
            UserName = "admin",
            Password = "admin123",
            Port = "8000",
            Channel = "1"
        });

        using var loader = new HikvisionPlateGoldenLoader();
        var replay = loader.PrepareReplay(entry, goldenRoot);

        service.InvokePlateAlarmCallbackForTests(
            replay.LCommand,
            replay.PAlarmer,
            replay.PAlarmInfo,
            replay.DwBufLen,
            WeighingMode.Standard);

        Assert.NotNull(captured);
        Assert.False(string.IsNullOrWhiteSpace(captured!.LprImagePath));
        Assert.True(PathManager.FileExists(captured.LprImagePath!),
            $"Standard + CameraConfigs 仍应保存 Lpr: {captured.LprImagePath}");
    }

    private static byte[] EncodeGbkLicense(string plate)
    {
        var gbk = Encoding.GetEncoding("GBK");
        var bytes = gbk.GetBytes(plate);
        var buffer = new byte[HikvisionSdk.MaxLicenseLen];
        Array.Copy(bytes, buffer, Math.Min(bytes.Length, buffer.Length));
        return buffer;
    }

    [Fact]
    public void GoldenManifest_MatchesCommittedBinarySizes()
    {
        var goldenRoot = HikvisionPlateGoldenLoader.ResolveGoldenRootDirectory();
        var manifest = HikvisionPlateGoldenLoader.LoadManifest(goldenRoot);

        foreach (var entry in manifest.Fixtures)
        {
            var dir = Path.Combine(goldenRoot, entry.Folder);
            var alarmerPath = Path.Combine(dir, entry.AlarmerFile);
            var alarmInfoPath = Path.Combine(dir, entry.AlarmInfoFile);

            Assert.True(File.Exists(alarmerPath), $"缺少 {alarmerPath}");
            Assert.True(File.Exists(alarmInfoPath), $"缺少 {alarmInfoPath}");

            var alarmerSize = new FileInfo(alarmerPath).Length;
            var alarmInfoSize = new FileInfo(alarmInfoPath).Length;

            Assert.Equal(372, alarmerSize);

            var expectedAlarmInfoSize = entry.LCommand == HikvisionSdk.COMM_UPLOAD_PLATE_RESULT ? 264 : 944;
            Assert.Equal(expectedAlarmInfoSize, alarmInfoSize);

            if (!string.IsNullOrEmpty(entry.ImageFile))
            {
                Assert.True(File.Exists(Path.Combine(dir, entry.ImageFile)), $"缺少图片 {entry.ImageFile}");
            }
        }
    }
}
