using System.Text;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.TruckScale.Protocols.Yaohua;
using NSubstitute;
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

    [Fact]
    public void BuildQuery_MatchesDemoLayout()
    {
        var frame = YaohuaTf1Protocol.BuildQuery('A', 'B');
        frame.Length.ShouldBe(6);
        frame[0].ShouldBe((byte)0x02);
        frame[1].ShouldBe((byte)'A');
        frame[2].ShouldBe((byte)'B');
        frame[5].ShouldBe((byte)0x03);

        var xor = 'A' ^ 'B';
        frame[3].ShouldBe((byte)(xor >> 4 <= 9 ? (xor >> 4) + 0x30 : (xor >> 4) + 0x37));
        frame[4].ShouldBe((byte)((xor & 0x0F) <= 9 ? (xor & 0x0F) + 0x30 : (xor & 0x0F) + 0x37));
    }

    [Fact]
    public void QueryInterval_IsTenSeconds()
    {
        YaohuaTf1Protocol.QueryInterval.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void TryParseCommandReply_ParsesGrossWeight()
    {
        // STX A B +123456 0 XOR XOR ETX — build with Demo checksum rules
        var payload = new byte[]
        {
            0x02,
            (byte)'A',
            (byte)'B',
            (byte)'+',
            (byte)'0', (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4',
            (byte)'0', // decimal places
            0x00, 0x00, // checksum placeholders
            0x03
        };
        var xor = 0;
        for (var i = 1; i < payload.Length - 3; i++)
            xor ^= payload[i];
        payload[^3] = (byte)(xor >> 4 <= 9 ? (xor >> 4) + 0x30 : (xor >> 4) + 0x37);
        payload[^2] = (byte)((xor & 0x0F) <= 9 ? (xor & 0x0F) + 0x30 : (xor & 0x0F) + 0x37);

        YaohuaTf1Protocol.TryParseCommandReply(payload, null, out var weight, out var command)
            .ShouldBeTrue();
        command.ShouldBe('B');
        weight.ShouldBe(1234m);
    }

    [Fact]
    public void Exchange_WritesThenReadsUntilEtx()
    {
        var request = YaohuaTf1Protocol.BuildQuery('A', 'B');
        var reply = new byte[]
        {
            0x02, (byte)'A', (byte)'B', (byte)'+',
            (byte)'0', (byte)'0', (byte)'0', (byte)'1', (byte)'0', (byte)'0',
            (byte)'0',
            0x00, 0x00, // checksum placeholders
            0x03
        };
        var xor = 0;
        for (var i = 1; i < reply.Length - 3; i++)
            xor ^= reply[i];
        reply[^3] = (byte)(xor >> 4 <= 9 ? (xor >> 4) + 0x30 : (xor >> 4) + 0x37);
        reply[^2] = (byte)((xor & 0x0F) <= 9 ? (xor & 0x0F) + 0x30 : (xor & 0x0F) + 0x37);

        var port = Substitute.For<ISerialPort>();
        port.IsOpen.Returns(true);
        var index = 0;
        port.ReadByte().Returns(_ =>
        {
            if (index >= reply.Length) throw new TimeoutException();
            return reply[index++];
        });

        var raw = YaohuaTf1Protocol.Exchange(port, request, replyTimeoutMs: 500);

        port.Received(1).DiscardInBuffer();
        port.Received(1).Write(request, 0, request.Length);
        raw.ShouldBe(reply);
        YaohuaTf1Protocol.TryParseCommandReply(raw, null, out var weight, out _).ShouldBeTrue();
        weight.ShouldBe(100m);
    }
}
