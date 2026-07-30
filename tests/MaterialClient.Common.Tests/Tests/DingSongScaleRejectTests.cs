using System.Reflection;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     Characterizes that ScaleType.DingSong (顶松) cannot parse scale samples
///     from <c>_temp/H610.txt</c> and <c>_temp/H1320.txt</c>.
///     Those frames use STX + '*' + CR (<c>02 2A ... 0D</c>), not DingSong's
///     12-byte <c>02 2B/2D ... 03</c> format.
/// </summary>
public class DingSongScaleRejectTests
{
    private readonly ISettingsService _mockSettingsService = Substitute.For<ISettingsService>();
    private readonly ILogger<TruckScaleWeightService> _mockLogger =
        Substitute.For<ILogger<TruckScaleWeightService>>();
    private readonly ISerialPortFactory _mockSerialPortFactory = Substitute.For<ISerialPortFactory>();

    /// <summary>
    ///     Single frame repeated in _temp/H610.txt (weight payload looks like 610).
    /// </summary>
    private static readonly byte[] H610Frame =
    [
        0x02, 0x2A, 0x30, 0x20,
        0x30, 0x30, 0x30, 0x36, 0x31, 0x30,
        0x30, 0x30, 0x30, 0x30, 0x30, 0x30,
        0x0D
    ];

    /// <summary>
    ///     Single frame repeated in _temp/H1320.txt (weight payload looks like 1330).
    /// </summary>
    private static readonly byte[] H1320Frame =
    [
        0x02, 0x2A, 0x30, 0x20,
        0x30, 0x30, 0x31, 0x33, 0x33, 0x30,
        0x30, 0x30, 0x30, 0x30, 0x30, 0x30,
        0x0D
    ];

    [Fact]
    public void ParseHexWeightDingSong_ShouldReturnNull_ForH610ScaleFrame()
    {
        H610Frame.Length.ShouldBe(17);
        H610Frame[0].ShouldBe((byte)0x02);
        H610Frame[1].ShouldBe((byte)0x2A); // '*' — not DingSong +/-
        H610Frame[^1].ShouldBe((byte)0x0D); // CR — not DingSong ETX 0x03

        InvokeParseHexWeightDingSong(H610Frame).ShouldBeNull();
    }

    [Fact]
    public void ParseHexWeightDingSong_ShouldReturnNull_ForH1320ScaleFrame()
    {
        H1320Frame.Length.ShouldBe(17);
        H1320Frame[0].ShouldBe((byte)0x02);
        H1320Frame[1].ShouldBe((byte)0x2A);
        H1320Frame[^1].ShouldBe((byte)0x0D);

        InvokeParseHexWeightDingSong(H1320Frame).ShouldBeNull();
    }

    [Theory]
    [InlineData(nameof(H610Frame))]
    [InlineData(nameof(H1320Frame))]
    public void ParseHexWeight_Default_ShouldAlsoReturnNull_ForStarCrScaleFrames(string frameName)
    {
        var frame = frameName == nameof(H610Frame) ? H610Frame : H1320Frame;
        InvokeParseHexWeight(frame).ShouldBeNull();
    }

    private decimal? InvokeParseHexWeightDingSong(byte[] buffer)
    {
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, _mockSerialPortFactory);
        var method = typeof(TruckScaleWeightService).GetMethod(
            "ParseHexWeightDingSong",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.ShouldNotBeNull();
        return (decimal?)method!.Invoke(service, [buffer]);
    }

    private decimal? InvokeParseHexWeight(byte[] buffer)
    {
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, _mockSerialPortFactory);
        var method = typeof(TruckScaleWeightService).GetMethod(
            "ParseHexWeight",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.ShouldNotBeNull();
        return (decimal?)method!.Invoke(service, [buffer]);
    }
}
