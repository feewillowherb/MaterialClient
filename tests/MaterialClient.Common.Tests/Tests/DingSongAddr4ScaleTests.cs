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
///     ScaleType.DingSongAddr4 parses 17-byte <c>02 2A … 0D</c> frames (H610 / H1320 style).
/// </summary>
public class DingSongAddr4ScaleTests
{
    private readonly ISettingsService _mockSettingsService = Substitute.For<ISettingsService>();
    private readonly ILogger<TruckScaleWeightService> _mockLogger =
        Substitute.For<ILogger<TruckScaleWeightService>>();
    private readonly ISerialPortFactory _mockSerialPortFactory = Substitute.For<ISerialPortFactory>();

    /// <summary>
    ///     Golden frame from H610.txt — first 6 payload digits = 000610 → 610 kg.
    /// </summary>
    private static readonly byte[] H610Frame =
    [
        0x02, 0x2A, 0x30, 0x20,
        0x30, 0x30, 0x30, 0x36, 0x31, 0x30,
        0x30, 0x30, 0x30, 0x30, 0x30, 0x30,
        0x0D
    ];

    /// <summary>
    ///     Golden frame for 1320 kg (contract). Not the on-disk H1320.txt bytes (001330).
    /// </summary>
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
        InvokeParseHexWeightDingSongAddr4(H610Frame).ShouldBe(610m);
    }

    [Fact]
    public void ParseHexWeightDingSongAddr4_H1320Golden_ShouldReturn1320Kg()
    {
        H1320GoldenFrame.Length.ShouldBe(17);
        InvokeParseHexWeightDingSongAddr4(H1320GoldenFrame).ShouldBe(1320m);
    }

    [Fact]
    public void ParseHexWeightDingSongAddr4_ShouldRejectInvalidTerminator()
    {
        var bad = (byte[])H610Frame.Clone();
        bad[^1] = 0x03;
        InvokeParseHexWeightDingSongAddr4(bad).ShouldBeNull();
    }

    [Fact]
    public async Task ReceiveHexDingSongAddr4_StickyTwoFrames_ShouldPublishBothWeights()
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
            ScaleType = ScaleType.DingSongAddr4,
            ScaleUnit = ScaleUnit.Kg
        };

        var receivedWeights = new System.Collections.Concurrent.ConcurrentBag<decimal>();
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

        var receiveMethod = typeof(TruckScaleWeightService).GetMethod(
            "ReceiveHexDingSongAddr4",
            BindingFlags.Instance | BindingFlags.NonPublic);
        receiveMethod.ShouldNotBeNull();
        receiveMethod!.Invoke(service, null);

        receivedWeights.ShouldContain(0.61m);  // 610 kg → ton via ConvertWeight
        receivedWeights.ShouldContain(1.32m); // 1320 kg → ton
        receivedWeights.Count.ShouldBe(2);

        await service.DisposeAsync();
    }

    private decimal? InvokeParseHexWeightDingSongAddr4(byte[] buffer)
    {
        var service = new TruckScaleWeightService(_mockLogger, _mockSettingsService, _mockSerialPortFactory);
        var method = typeof(TruckScaleWeightService).GetMethod(
            "ParseHexWeightDingSongAddr4",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.ShouldNotBeNull();
        return (decimal?)method!.Invoke(service, [buffer]);
    }
}
