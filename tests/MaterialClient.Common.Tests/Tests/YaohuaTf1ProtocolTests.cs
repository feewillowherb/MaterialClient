using System.Text;
using MaterialClient.Common.Services.TruckScale.Protocols.Yaohua;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class YaohuaTf1ProtocolTests
{
    [Fact]
    public void TryParseExtendedFrame_ParsesGrossAndTare()
    {
        // STX + Gross(6)=400000 + Tare(6)=015000 + status + CR LF
        var frame = Encoding.ASCII.GetBytes("\u0002400000015000\u0001\r\n");
        YaohuaTf1Protocol.TryParseExtendedFrame(frame, out var gross, out var tare).ShouldBeTrue();
        gross.ShouldBe(400000m);
        tare.ShouldBe(15000m);
    }

    [Fact]
    public void TryParseStandardFrame_UsesTf0Layout()
    {
        // Classic 12-byte: STX, sign, 6 digits-ish, E, status?, ETX — reuse Tf0 sample shape
        var frame = new byte[]
        {
            0x02, 0x2B, 0x30, 0x30, 0x31, 0x32, 0x33, 0x34, 0x45, 0x30, 0x30, 0x03
        };
        YaohuaTf1Protocol.TryParseStandardFrame(frame, null, out var weight).ShouldBeTrue();
        weight.ShouldBe(1234m);
    }

    [Fact]
    public void TryParseExtendedFrame_RejectsClassicEtxFrame()
    {
        var frame = new byte[]
        {
            0x02, 0x2B, 0x30, 0x30, 0x31, 0x32, 0x33, 0x34, 0x45, 0x30, 0x30, 0x03
        };
        YaohuaTf1Protocol.TryParseExtendedFrame(frame, out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void ResolveAddress_DefaultsToA()
    {
        YaohuaCommunicationParameter.ResolveAddressOrThrow(null).ShouldBe('A');
        YaohuaCommunicationParameter.ResolveAddressOrThrow("b").ShouldBe('B');
        Should.Throw<ArgumentException>(() => YaohuaCommunicationParameter.ResolveAddressOrThrow("9"));
    }
}
