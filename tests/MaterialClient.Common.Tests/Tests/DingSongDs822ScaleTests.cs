using System.Reflection;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     ScaleType.DingSong parses DS822-X continuous A/C frames (not legacy 12-byte).
/// </summary>
public class DingSongDs822ScaleTests
{
    private readonly ISettingsService _mockSettingsService = Substitute.For<ISettingsService>();
    private readonly ILogger<TruckScaleWeightService> _mockLogger =
        Substitute.For<ILogger<TruckScaleWeightService>>();
    private readonly ISerialPortFactory _mockSerialPortFactory = Substitute.For<ISerialPortFactory>();

    /// <summary>Field golden from _tmp/text.txt — display digits → 8.</summary>
    private static readonly byte[] FieldCFrame =
    [
        0x02, 0x41, 0x63, 0x30, 0x20, 0x30, 0x20, 0x30, 0x20, 0x30, 0x20, 0x38, 0x20, 0x3C, 0x30, 0x74, 0x03
    ];

    private static readonly byte[] Legacy12ByteFrame =
    [
        0x02, 0x2B, 0x30, 0x30, 0x31, 0x30, 0x30, 0x30, 0x30, 0x30, 0x42, 0x03
    ];

    private static readonly byte[] H610Frame =
    [
        0x02, 0x2A, 0x30, 0x20,
        0x30, 0x30, 0x30, 0x36, 0x31, 0x30,
        0x30, 0x30, 0x30, 0x30, 0x30, 0x30,
        0x0D
    ];

    [Fact]
    public void TryParse_FieldCFrame_ShouldReturn8()
    {
        var parsed = DingSongDs822Frame.TryParse(FieldCFrame);
        parsed.ShouldNotBeNull();
        parsed!.WeightKg.ShouldBe(8m);
        parsed.Address.ShouldBe('A');
        parsed.Command.ShouldBe('c');
    }

    [Fact]
    public void TryParse_AFrame_ShouldReturnNetWeightWithDecimals()
    {
        // +000130, p=2 → 1.30; tare ignored
        var frame = DingSongDs822Frame.BuildAFrameForTests('A', 'A', '+', "000130", 2);
        frame.Length.ShouldBe(22);
        var parsed = DingSongDs822Frame.TryParse(frame);
        parsed.ShouldNotBeNull();
        parsed!.WeightKg.ShouldBe(1.30m);
    }

    [Fact]
    public void TryParse_AFrame_BadChecksum_ShouldReturnNull()
    {
        var frame = DingSongDs822Frame.BuildAFrameForTests('A', 'a', '+', "000100", 0);
        frame[^2] ^= 0x01;
        DingSongDs822Frame.TryParse(frame).ShouldBeNull();
    }

    [Fact]
    public void TryParse_CFrame_BadChecksum_ShouldReturnNull()
    {
        var bad = (byte[])FieldCFrame.Clone();
        bad[^2] ^= 0x01;
        DingSongDs822Frame.TryParse(bad).ShouldBeNull();
    }

    [Fact]
    public void ParseHexWeightDingSong_Legacy12Byte_ShouldReturnNull()
    {
        InvokeParseHexWeightDingSong(Legacy12ByteFrame).ShouldBeNull();
    }

    [Fact]
    public void ParseHexWeightDingSong_H610_ShouldReturnNull()
    {
        InvokeParseHexWeightDingSong(H610Frame).ShouldBeNull();
    }

    [Fact]
    public void ParseHexWeightDingSong_FieldC_ShouldReturn8()
    {
        InvokeParseHexWeightDingSong(FieldCFrame).ShouldBe(8m);
    }

    [Fact]
    public async Task ReceiveHexDingSong_StickyCThenA_ShouldPublishBothWeights()
    {
        var mockSerialPort = Substitute.For<ISerialPort>();
        var mockFactory = Substitute.For<ISerialPortFactory>();
        mockFactory.Create().Returns(mockSerialPort);

        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, mockFactory);
        var settings = new ScaleSettings
        {
            SerialPort = "COM3",
            BaudRate = "9600",
            CommunicationMethod = "TF0",
            ScaleType = ScaleType.DingSong,
            ScaleUnit = ScaleUnit.Kg
        };

        var receivedWeights = new System.Collections.Concurrent.ConcurrentBag<decimal>();
        using var subscription = service.WeightUpdates.Subscribe(w => receivedWeights.Add(w));

        mockSerialPort.IsOpen.Returns(true);
        mockSerialPort.When(x => x.Open()).Do(_ => { });
        await service.InitializeAsync(settings);

        // A frame: +000200 p=0 → 200 kg → 0.2 ton after ConvertWeight
        var aFrame = DingSongDs822Frame.BuildAFrameForTests('A', 'A', '+', "000200", 0);
        var sticky = new byte[FieldCFrame.Length + aFrame.Length];
        Buffer.BlockCopy(FieldCFrame, 0, sticky, 0, FieldCFrame.Length);
        Buffer.BlockCopy(aFrame, 0, sticky, FieldCFrame.Length, aFrame.Length);

        mockSerialPort.BytesToRead.Returns(sticky.Length);
        mockSerialPort.Read(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(ci =>
            {
                var buffer = ci.ArgAt<byte[]>(0);
                var start = ci.ArgAt<int>(1);
                var count = ci.ArgAt<int>(2);
                var toCopy = Math.Min(count, sticky.Length);
                Array.Copy(sticky, 0, buffer, start, toCopy);
                return toCopy;
            });

        var receiveMethod = typeof(TruckScaleWeightService).GetMethod(
            "ReceiveHexDingSong",
            BindingFlags.Instance | BindingFlags.NonPublic);
        receiveMethod.ShouldNotBeNull();
        receiveMethod!.Invoke(service, null);

        receivedWeights.ShouldContain(0.01m); // 8 kg → ton (ConvertKgToTon rounds to 2 dp)
        receivedWeights.ShouldContain(0.2m);  // 200 kg → ton
        receivedWeights.Count.ShouldBe(2);

        await service.DisposeAsync();
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
}
