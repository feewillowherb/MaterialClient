using System.Collections.Concurrent;
using System.IO.Ports;
using System.Reflection;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.TruckScale.Facade;
using MaterialClient.Common.Services.TruckScale.Protocols.DingSongAddr4;
using MaterialClient.Common.Services.TruckScale.Routing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
///     ScaleType.DingSongAddr4 parses 17-byte 02 2A … 0D frames.
/// </summary>
public class DingSongAddr4ScaleTests
{
    private readonly ISettingsService _mockSettingsService = Substitute.For<ISettingsService>();
    private readonly ILogger<TruckScaleWeightService> _mockLogger =
        Substitute.For<ILogger<TruckScaleWeightService>>();

    private static readonly byte[] H610Frame =
    [
        0x02, 0x2A, 0x30, 0x20,
        0x30, 0x30, 0x30, 0x36, 0x31, 0x30,
        0x30, 0x30, 0x30, 0x30, 0x30, 0x30,
        0x0D
    ];

    private static readonly byte[] H1320GoldenFrame =
    [
        0x02, 0x2A, 0x30, 0x20,
        0x30, 0x30, 0x31, 0x33, 0x32, 0x30,
        0x30, 0x30, 0x30, 0x30, 0x30, 0x30,
        0x0D
    ];

    [Fact]
    public void ParseHexWeightDingSongAddr4_H610_ShouldReturn610Kg()
    {
        H610Frame.Length.ShouldBe(17);
        DingSongAddr4Tf0Protocol.ParseHexWeight(H610Frame, logger: null).ShouldBe(610m);
    }

    [Fact]
    public void ParseHexWeightDingSongAddr4_H1320Golden_ShouldReturn1320Kg()
    {
        H1320GoldenFrame.Length.ShouldBe(17);
        DingSongAddr4Tf0Protocol.ParseHexWeight(H1320GoldenFrame, logger: null).ShouldBe(1320m);
    }

    [Fact]
    public void ParseHexWeightDingSongAddr4_ShouldRejectInvalidTerminator()
    {
        var bad = (byte[])H610Frame.Clone();
        bad[^1] = 0x03;
        DingSongAddr4Tf0Protocol.ParseHexWeight(bad, logger: null).ShouldBeNull();
    }

    [Fact]
    public async Task ReceiveHexDingSongAddr4_StickyTwoFrames_ShouldPublishBothWeights()
    {
        var mockSerialPort = Substitute.For<ISerialPort>();
        var mockFactory = Substitute.For<ISerialPortFactory>();
        mockFactory.Create().Returns(mockSerialPort);

        var service = new TruckScaleWeightService(
            _mockLogger,
            _mockSettingsService,
            mockFactory,
            new TruckScaleProtocolRouter());
        var settings = new ScaleSettings
        {
            SerialPort = "COM3",
            BaudRate = "9600",
            TransmissionFormatType = TransmissionFormatType.TransmissionFormatType0,
            ScaleType = ScaleType.DingSongAddr4,
            ScaleUnit = ScaleUnit.Kg
        };

        var receivedWeights = new ConcurrentBag<decimal>();
        using var subscription = service.WeightUpdates.Subscribe(w => receivedWeights.Add(w));

        mockSerialPort.IsOpen.Returns(true);
        mockSerialPort.When(x => x.Open()).Do(_ => { });
        await service.InitializeAsync(settings);

        var sticky = new byte[H610Frame.Length + H1320GoldenFrame.Length];
        Buffer.BlockCopy(H610Frame, 0, sticky, 0, H610Frame.Length);
        Buffer.BlockCopy(H1320GoldenFrame, 0, sticky, H610Frame.Length, H1320GoldenFrame.Length);

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

        RaiseSerialDataReceived(mockSerialPort);

        receivedWeights.ShouldContain(0.61m);
        receivedWeights.ShouldContain(1.32m);
        receivedWeights.Count.ShouldBe(2);

        await service.DisposeAsync();
    }

    private static void RaiseSerialDataReceived(ISerialPort mockSerialPort)
    {
        var eventArgs = (SerialDataReceivedEventArgs)Activator.CreateInstance(
            typeof(SerialDataReceivedEventArgs),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            [SerialData.Chars],
            null)!;

        mockSerialPort.DataReceived += Raise.Event<SerialDataReceivedEventHandler>(
            mockSerialPort,
            eventArgs);
    }
}
