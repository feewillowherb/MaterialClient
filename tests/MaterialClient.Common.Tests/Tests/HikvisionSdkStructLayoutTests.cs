using System.Runtime.InteropServices;
using MaterialClient.Common.Services.Hikvision;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     海康 SDK V6.1.9.48 LPR 结构体布局防回归测试。
///     字段定义与 Fdsoft.Weight.GovClient/BLL/CHCNetSDK.cs 同版本一致；
///     SizeOf 期望值为 win-x64（IntPtr=8）下的实测值，用于检测字段顺序/类型被误改。
/// </summary>
public class HikvisionSdkStructLayoutTests
{
    private readonly ITestOutputHelper _output;

    public HikvisionSdkStructLayoutTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(typeof(HikvisionSdk.NET_DVR_TIME_V30), 12)]
    [InlineData(typeof(HikvisionSdk.NET_VCA_RECT), 16)]
    [InlineData(typeof(HikvisionSdk.NET_DVR_PLATE_INFO), 96)]
    [InlineData(typeof(HikvisionSdk.NET_DVR_VEHICLE_INFO), 48)]
    [InlineData(typeof(HikvisionSdk.NET_DVR_ALARMER), 372)]
    [InlineData(typeof(HikvisionSdk.NET_ITS_PICTURE_INFO), 104)]
    [InlineData(typeof(HikvisionSdk.NET_DVR_PLATE_RESULT), 264)]
    [InlineData(typeof(HikvisionSdk.NET_ITS_PLATE_RESULT), 944)]
    public void StructSize_MatchesOfficialChcNetSdk(Type structType, int expectedSize)
    {
        var actualSize = Marshal.SizeOf(structType);
        _output.WriteLine($"{structType.Name}: expected={expectedSize}, actual={actualSize}");
        Assert.Equal(expectedSize, actualSize);
    }
}
