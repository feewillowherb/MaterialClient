using System.Runtime.InteropServices;
using System.Text;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Utils;

namespace MaterialClient.Common.Tests.HikvisionGolden;

/// <summary>
///     按 HCNetSDK V6.1.9.48 win-x64 布局合成 Golden binary（可替换为真机 dump）。
/// </summary>
public static class HikvisionPlateGoldenFixtureBuilder
{
    public const string DefaultDeviceIp = "192.168.1.100";
    public const string DefaultPlate = "浙A12345";

    /// <summary>
    ///     最小合法 JPEG（1x1），用于 Lpr 附件保存测试。
    /// </summary>
    public static readonly byte[] MinimalSceneJpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////2wBDAf//////////////////////////////////////////////////////////////////////////////////////wAARCAABAAEDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAv/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/8QAFQEBAQAAAAAAAAAAAAAAAAAAAAX/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIRAxEAPwCwAA8A/9k=");

    public static void WriteAllFixtures(string goldenRootDirectory)
    {
        CodePagesEncodingInitializer.Register();
        Directory.CreateDirectory(Path.Combine(goldenRootDirectory, "upload"));
        Directory.CreateDirectory(Path.Combine(goldenRootDirectory, "its"));

        WriteUploadFixture(goldenRootDirectory);
        WriteItsFixture(goldenRootDirectory);
        WriteManifest(goldenRootDirectory);
    }

    private static void WriteManifest(string goldenRootDirectory)
    {
        var manifest = new HikvisionPlateGoldenManifest
        {
            Fixtures =
            [
                new HikvisionPlateGoldenFixtureEntry
                {
                    Id = "upload_yellow_plate",
                    Folder = "upload",
                    LCommand = HikvisionSdk.COMM_UPLOAD_PLATE_RESULT,
                    DeviceIp = DefaultDeviceIp,
                    DeviceName = "入口相机",
                    ExpectedPlate = DefaultPlate,
                    ExpectedPlateColor = "黄",
                    ExpectedVehicleColor = "白色",
                    ExpectedVehicleType = "小轿车",
                    AlarmerFile = "alarmer.bin",
                    AlarmInfoFile = "plate_result.bin",
                    ImageFile = "scene.jpg",
                    ImageBinding = new HikvisionPlateGoldenImageBinding { Type = "upload" }
                },
                new HikvisionPlateGoldenFixtureEntry
                {
                    Id = "its_yellow_plate",
                    Folder = "its",
                    LCommand = HikvisionSdk.COMM_ITS_PLATE_RESULT,
                    DeviceIp = DefaultDeviceIp,
                    DeviceName = "入口相机",
                    ExpectedPlate = DefaultPlate,
                    ExpectedPlateColor = "黄",
                    ExpectedVehicleColor = "白色",
                    ExpectedVehicleType = "小轿车",
                    AlarmerFile = "alarmer.bin",
                    AlarmInfoFile = "plate_result.bin",
                    ImageFile = "scene.jpg",
                    ImageBinding = new HikvisionPlateGoldenImageBinding { Type = "its", PicIndex = 0 }
                }
            ]
        };

        var json = System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(Path.Combine(goldenRootDirectory, HikvisionPlateGoldenManifest.ManifestFileName), json);
    }

    private static void WriteUploadFixture(string goldenRootDirectory)
    {
        var folder = Path.Combine(goldenRootDirectory, "upload");
        WriteBinary(Path.Combine(folder, "alarmer.bin"), CreateAlarmer(DefaultDeviceIp));
        WriteBinary(Path.Combine(folder, "plate_result.bin"), CreateUploadPlateResult(DefaultPlate));
        File.WriteAllBytes(Path.Combine(folder, "scene.jpg"), MinimalSceneJpeg);
    }

    private static void WriteItsFixture(string goldenRootDirectory)
    {
        var folder = Path.Combine(goldenRootDirectory, "its");
        WriteBinary(Path.Combine(folder, "alarmer.bin"), CreateAlarmer(DefaultDeviceIp));
        WriteBinary(Path.Combine(folder, "plate_result.bin"), CreateItsPlateResult(DefaultPlate));
        File.WriteAllBytes(Path.Combine(folder, "scene.jpg"), MinimalSceneJpeg);
    }

    private static HikvisionSdk.NET_DVR_ALARMER CreateAlarmer(string deviceIp)
    {
        return new HikvisionSdk.NET_DVR_ALARMER
        {
            byDeviceIPValid = 1,
            sDeviceIP = FixedUtf8(deviceIp, 128),
            sSerialNumber = new byte[HikvisionSdk.SerialNoLen],
            sDeviceName = FixedUtf8("iDS-TCM204-E", HikvisionSdk.NameLen),
            byMacAddr = new byte[HikvisionSdk.MacAddrLen],
            sSocketIP = new byte[128],
            byRes2 = new byte[11]
        };
    }

    private static HikvisionSdk.NET_DVR_PLATE_RESULT CreateUploadPlateResult(string plate)
    {
        return new HikvisionSdk.NET_DVR_PLATE_RESULT
        {
            dwSize = (uint)Marshal.SizeOf<HikvisionSdk.NET_DVR_PLATE_RESULT>(),
            byAbsTime = Encoding.ASCII.GetBytes("20260702120000000").Concat(new byte[32]).Take(32).ToArray(),
            byRes3 = new byte[6],
            byVehicleType = 1,
            struPlateInfo = BuildPlateInfo(plate, plateColor: 2),
            struVehicleInfo = BuildVehicleInfo(vehicleColor: 1, vehicleType: 1),
            dwPicLen = 0,
            pBuffer1 = IntPtr.Zero
        };
    }

    private static HikvisionSdk.NET_ITS_PLATE_RESULT CreateItsPlateResult(string plate)
    {
        return new HikvisionSdk.NET_ITS_PLATE_RESULT
        {
            dwSize = (uint)Marshal.SizeOf<HikvisionSdk.NET_ITS_PLATE_RESULT>(),
            byIllegalSubType = new byte[8],
            struPlateInfo = BuildPlateInfo(plate, plateColor: 2),
            struVehicleInfo = BuildVehicleInfo(vehicleColor: 1, vehicleType: 1),
            byMonitoringSiteID = new byte[48],
            byDeviceID = new byte[48],
            struSnapFirstPicTime = new HikvisionSdk.NET_DVR_TIME_V30 { byRes1 = new byte[2] },
            dwPicNum = 1,
            struPicInfo =
            [
                BuildItsPictureInfoSlot(),
                BuildItsPictureInfoSlot(),
                BuildItsPictureInfoSlot(),
                BuildItsPictureInfoSlot(),
                BuildItsPictureInfoSlot(),
                BuildItsPictureInfoSlot()
            ]
        };
    }

    private static HikvisionSdk.NET_DVR_PLATE_INFO BuildPlateInfo(string plate, byte plateColor)
    {
        return new HikvisionSdk.NET_DVR_PLATE_INFO
        {
            byColor = plateColor,
            byRes = new byte[15],
            sPlateCategory = new byte[HikvisionSdk.MaxCategoryLen],
            sLicense = FixedGbkLicense(plate),
            byBelieve = new byte[HikvisionSdk.MaxLicenseLen]
        };
    }

    private static HikvisionSdk.NET_DVR_VEHICLE_INFO BuildVehicleInfo(byte vehicleColor, byte vehicleType)
    {
        return new HikvisionSdk.NET_DVR_VEHICLE_INFO
        {
            byColor = vehicleColor,
            byVehicleType = vehicleType,
            byCustomInfo = new byte[16],
            byRes3 = new byte[4]
        };
    }

    private static HikvisionSdk.NET_ITS_PICTURE_INFO BuildItsPictureInfoSlot()
    {
        return new HikvisionSdk.NET_ITS_PICTURE_INFO
        {
            byRes1 = new byte[3],
            byAbsTime = new byte[32],
            byRes2 = new byte[12],
            byType = HikvisionSdk.HikItsPictureTypeScene,
            dwDataLen = 0,
            pBuffer = IntPtr.Zero
        };
    }

    private static byte[] FixedUtf8(string text, int length)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var buffer = new byte[length];
        Array.Copy(bytes, buffer, Math.Min(bytes.Length, length));
        return buffer;
    }

    private static byte[] FixedGbkLicense(string plate)
    {
        var gbk = Encoding.GetEncoding("GBK");
        var bytes = gbk.GetBytes(plate);
        var buffer = new byte[HikvisionSdk.MaxLicenseLen];
        Array.Copy(bytes, buffer, Math.Min(bytes.Length, buffer.Length));
        return buffer;
    }

    private static void WriteBinary<T>(string path, T value) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(value, ptr, false);
            var bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);
            File.WriteAllBytes(path, bytes);
        }
        finally
        {
            Marshal.DestroyStructure(ptr, typeof(T));
            Marshal.FreeHGlobal(ptr);
        }
    }
}
