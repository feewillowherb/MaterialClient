using System.Text.Json;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services.TruckScale.Protocols;
using MaterialClient.Common.Services.TruckScale.Protocols.DingSong;
using MaterialClient.Common.Services.TruckScale.Protocols.TestMode;
using MaterialClient.Common.Services.TruckScale.Protocols.Unsupported;
using MaterialClient.Common.Services.TruckScale.Protocols.Yaohua;
using MaterialClient.Common.Services.TruckScale.Routing;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class TruckScaleProtocolRouterTests
{
    private readonly TruckScaleProtocolRouter _router = new();

    [Theory]
    [InlineData(ScaleType.Yaohua, typeof(YaohuaTf0Protocol))]
    [InlineData(ScaleType.DingSong, typeof(DingSongTf0Protocol))]
    [InlineData(ScaleType.TestMode, typeof(TestModeTf0Protocol))]
    public void Resolve_Type0_ReturnsDeviceProtocol(ScaleType scaleType, Type expectedType)
    {
        var protocol = _router.Resolve(scaleType, TransmissionFormatType.TransmissionFormatType0);
        protocol.ShouldBeOfType(expectedType);
    }

    [Theory]
    [InlineData(ScaleType.Yaohua)]
    [InlineData(ScaleType.DingSong)]
    [InlineData(ScaleType.TestMode)]
    [InlineData(ScaleType.PortableXPSY)]
    [InlineData(ScaleType.DingSongAddr4)]
    public void Resolve_Type1_ReturnsUnsupported(ScaleType scaleType)
    {
        var protocol = _router.Resolve(scaleType, TransmissionFormatType.TransmissionFormatType1);
        protocol.ShouldBeOfType<UnsupportedTransmissionFormatProtocol>();
        Should.Throw<UnsupportedTransmissionFormatException>(() =>
            protocol.EnsureSupported(scaleType, TransmissionFormatType.TransmissionFormatType1));
    }

    [Fact]
    public void LegacyJson_WithoutTransmissionFormatType_DefaultsToType0()
    {
        const string json = """
            {"SerialPort":"COM3","BaudRate":"9600","CommunicationMethod":"TF1","ScaleUnit":0,"ScaleType":0}
            """;

        var settings = JsonSerializer.Deserialize<ScaleSettings>(json);
        settings.ShouldNotBeNull();
        settings!.TransmissionFormatType.ShouldBe(TransmissionFormatType.TransmissionFormatType0);
    }
}
