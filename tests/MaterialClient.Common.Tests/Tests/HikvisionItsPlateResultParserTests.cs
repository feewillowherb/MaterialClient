using System.Runtime.InteropServices;
using System.Text;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Utils;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     NET_ITS_PLATE_RESULT (COMM_ITS_PLATE_RESULT) 结构体编组与解析测试
/// </summary>
public class HikvisionItsPlateResultParserTests
{
    private readonly ITestOutputHelper _output;

    public HikvisionItsPlateResultParserTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void NET_ITS_PLATE_RESULT_ShouldMatchSdkLayoutSize592Bytes()
    {
        var size = Marshal.SizeOf<HikvisionSdk.NET_ITS_PLATE_RESULT>();
        _output.WriteLine($"Marshal.SizeOf<NET_ITS_PLATE_RESULT>() = {size}");
        Assert.Equal(592, size);
    }

    [Fact]
    public void Parse_ZheA88666Sample_ShouldProduceExpectedDiagnosticText()
    {
        var expected = """
            【COMM_ITS_PLATE_RESULT 车牌抓拍上报】
            结构体长度：592
            车牌号(GBK)：浙A88666
            车牌颜色：蓝色(0)
            车牌类型：普通蓝牌(0)
            识别置信度：96
            车身颜色：银色(5)
            车牌坐标：左220 上150 右450 下220
            车辆类型：小型轿车(1)
            车道号：1
            数据类型：实时过车(0)
            抓拍时间：2026-07-01 10:20:35
            违法时长：0ms
            抓拍图片总数：2张
            图片1：类型=1(全景图)，图片大小=102400字节，抓拍时间2026-07-01 10:20:35
            图片2：类型=2(车牌特写)，图片大小=40960字节，抓拍时间2026-07-01 10:20:35
            """;

        var sample = CreateZheA88666Sample();
        var nativeBytes = new byte[sample.Size];
        Marshal.Copy(sample.Pointer, nativeBytes, 0, nativeBytes.Length);

        try
        {
            var parsed = HikvisionItsPlateResultParser.Parse(sample.Pointer);
            var actual = HikvisionItsPlateResultParser.Format(parsed);

            _output.WriteLine(actual);
            Assert.Equal(expected, actual);
            Assert.Equal("浙A88666", HikvisionEncodingHelper.GetString(parsed.struPlateInfo.sLicense));
            Assert.Equal(2u, parsed.dwPicNum);
            Assert.Equal((byte)1, parsed.byDriveChan);
        }
        finally
        {
            sample.Dispose();
        }
    }

    private static NativeItsPlateSample CreateZheA88666Sample()
    {
        var overviewImage = new byte[102400];
        var closeUpImage = new byte[40960];
        var overviewHandle = GCHandle.Alloc(overviewImage, GCHandleType.Pinned);
        var closeUpHandle = GCHandle.Alloc(closeUpImage, GCHandleType.Pinned);

        var result = new HikvisionSdk.NET_ITS_PLATE_RESULT
        {
            dwSize = 592,
            byDriveChan = 1,
            byVehicleType = 1,
            byAlarmDataType = 0,
            byBarrierGateCtrlType = 0,
            struPlateInfo = new HikvisionSdk.NET_DVR_PLATE_INFO
            {
                byPlateType = 0,
                byColor = 0,
                byEntireBelieve = 96,
                byRes = new byte[15],
                sPlateCategory = new byte[HikvisionSdk.MaxCategoryLen],
                sLicense = ToFixedGbkBytes("浙A88666", HikvisionSdk.MaxLicenseLen),
                byBelieve = new byte[HikvisionSdk.MaxLicenseLen],
                struPlateRect = new HikvisionSdk.NET_VCA_RECT
                {
                    fX = 220,
                    fY = 150,
                    fWidth = 450,
                    fHeight = 220
                }
            },
            struVehicleInfo = new HikvisionSdk.NET_DVR_VEHICLE_INFO
            {
                byColor = 5,
                byCustomInfo = new byte[16],
                byRes3 = new byte[4]
            },
            byMonitoringSiteID = new byte[48],
            byDeviceID = new byte[48],
            byIllegalSubType = new byte[8],
            struSnapFirstPicTime = new HikvisionSdk.NET_DVR_TIME_V30
            {
                wYear = 2026,
                byMonth = 7,
                byDay = 1,
                byHour = 10,
                byMinute = 20,
                bySecond = 35,
                wMilliSec = 0,
                byRes1 = new byte[2]
            },
            dwIllegalTime = 0,
            dwPicNum = 2,
            struPicInfo = CreatePictureInfoArray(
                overviewHandle.AddrOfPinnedObject(),
                102400,
                1,
                closeUpHandle.AddrOfPinnedObject(),
                40960,
                2)
        };

        var structSize = Marshal.SizeOf<HikvisionSdk.NET_ITS_PLATE_RESULT>();
        var pointer = Marshal.AllocHGlobal(structSize);
        Marshal.StructureToPtr(result, pointer, false);

        return new NativeItsPlateSample(pointer, structSize, overviewHandle, closeUpHandle);
    }

    private static HikvisionSdk.NET_ITS_PICTURE_INFO[] CreatePictureInfoArray(
        IntPtr overviewBuffer,
        uint overviewLength,
        byte overviewType,
        IntPtr closeUpBuffer,
        uint closeUpLength,
        byte closeUpType)
    {
        var absTime = Encoding.ASCII.GetBytes("20260701102035000");
        var absTimeBuffer = new byte[32];
        Array.Copy(absTime, absTimeBuffer, Math.Min(absTime.Length, absTimeBuffer.Length));

        var pictures = new HikvisionSdk.NET_ITS_PICTURE_INFO[HikvisionSdk.ItsPictureInfoCount];
        for (var i = 0; i < pictures.Length; i++)
        {
            pictures[i] = new HikvisionSdk.NET_ITS_PICTURE_INFO
            {
                byRes1 = new byte[3],
                byAbsTime = new byte[32],
                byRes2 = new byte[12],
                struPlateRect = new HikvisionSdk.NET_VCA_RECT(),
                struPlateRecgRect = new HikvisionSdk.NET_VCA_RECT()
            };
        }

        pictures[0] = new HikvisionSdk.NET_ITS_PICTURE_INFO
        {
            dwDataLen = overviewLength,
            byType = overviewType,
            byRes1 = new byte[3],
            byAbsTime = (byte[])absTimeBuffer.Clone(),
            pBuffer = overviewBuffer,
            byRes2 = new byte[12],
            struPlateRect = new HikvisionSdk.NET_VCA_RECT(),
            struPlateRecgRect = new HikvisionSdk.NET_VCA_RECT()
        };

        pictures[1] = new HikvisionSdk.NET_ITS_PICTURE_INFO
        {
            dwDataLen = closeUpLength,
            byType = closeUpType,
            byRes1 = new byte[3],
            byAbsTime = (byte[])absTimeBuffer.Clone(),
            pBuffer = closeUpBuffer,
            byRes2 = new byte[12],
            struPlateRect = new HikvisionSdk.NET_VCA_RECT(),
            struPlateRecgRect = new HikvisionSdk.NET_VCA_RECT()
        };

        return pictures;
    }

    private static byte[] ToFixedGbkBytes(string text, int fixedLength)
    {
        var bytes = HikvisionEncodingHelper.GetBytes(text);
        var buffer = new byte[fixedLength];
        Array.Copy(bytes, buffer, Math.Min(bytes.Length, fixedLength));
        return buffer;
    }

    private sealed class NativeItsPlateSample : IDisposable
    {
        private readonly GCHandle _overviewHandle;
        private readonly GCHandle _closeUpHandle;
        private bool _disposed;

        public NativeItsPlateSample(IntPtr pointer, int size, GCHandle overviewHandle, GCHandle closeUpHandle)
        {
            Pointer = pointer;
            Size = size;
            _overviewHandle = overviewHandle;
            _closeUpHandle = closeUpHandle;
        }

        public IntPtr Pointer { get; }

        public int Size { get; }

        public void Dispose()
        {
            if (_disposed)
                return;

            Marshal.FreeHGlobal(Pointer);
            if (_overviewHandle.IsAllocated)
                _overviewHandle.Free();
            if (_closeUpHandle.IsAllocated)
                _closeUpHandle.Free();

            _disposed = true;
        }
    }
}
