using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.TruckScale.Protocols.DingSong;
using MaterialClient.Common.Services.TruckScale.Protocols.Yaohua;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     Characterizes that ScaleType.DingSong cannot parse scale samples
///     from H610/H1320 style frames (STX + '*' + CR).
/// </summary>
public class DingSongScaleRejectTests
{
    private static readonly byte[] H610Frame =
    [
        0x02, 0x2A, 0x30, 0x20,
        0x30, 0x30, 0x30, 0x36, 0x31, 0x30,
        0x30, 0x30, 0x30, 0x30, 0x30, 0x30,
        0x0D
    ];

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
        H610Frame[1].ShouldBe((byte)0x2A);
        H610Frame[^1].ShouldBe((byte)0x0D);

        DingSongTf0Protocol.ParseHexWeight(H610Frame, logger: null).ShouldBeNull();
    }

    [Fact]
    public void ParseHexWeightDingSong_ShouldReturnNull_ForH1320ScaleFrame()
    {
        H1320Frame.Length.ShouldBe(17);
        DingSongTf0Protocol.ParseHexWeight(H1320Frame, logger: null).ShouldBeNull();
    }

    [Theory]
    [InlineData(nameof(H610Frame))]
    [InlineData(nameof(H1320Frame))]
    public void ParseHexWeight_Default_ShouldAlsoReturnNull_ForStarCrScaleFrames(string frameName)
    {
        var frame = frameName == nameof(H610Frame) ? H610Frame : H1320Frame;
        YaohuaTf0Protocol.ParseHexWeight(frame, logger: null).ShouldBeNull();
    }
}
