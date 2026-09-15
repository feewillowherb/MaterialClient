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

    [Fact]
    public void Resolve_YaohuaType1_ReturnsContinuousQueryProtocol()
    {
        var protocol = _router.Resolve(ScaleType.Yaohua, TransmissionFormatType.TransmissionFormatType1);
        protocol.ShouldBeOfType<YaohuaTf1Protocol>();
        Should.NotThrow(() =>
            protocol.EnsureSupported(ScaleType.Yaohua, TransmissionFormatType.TransmissionFormatType1));
    }

    [Theory]
    [InlineData(ScaleType.DingSong)]
    [InlineData(ScaleType.TestMode)]
    [InlineData(ScaleType.PortableXPSY)]
    [InlineData(ScaleType.DingSongAddr4)]
    public void Resolve_NonYaohuaType1_ReturnsUnsupported(ScaleType scaleType)
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

    [Fact]
    public void ScaleSettings_ApplyTransmissionFormatChange_ResetsYaohuaParameterToA()
    {
        var settings = new ScaleSettings
        {
            ScaleType = ScaleType.Yaohua,
            TransmissionFormatType = TransmissionFormatType.TransmissionFormatType0,
            CommunicationParameter = "Z"
        };

        settings.ApplyTransmissionFormatChange(TransmissionFormatType.TransmissionFormatType1);
        settings.TransmissionFormatType.ShouldBe(TransmissionFormatType.TransmissionFormatType1);
        settings.CommunicationParameter.ShouldBe("A");
    }

    [Fact]
    public void ScaleComponentWeights_AnyMissing_IsNotAllValid()
    {
        new ScaleComponentWeights(1m, 2m, null).AllValid.ShouldBeFalse();
        new ScaleComponentWeights(1m, null, 3m).AllValid.ShouldBeFalse();
        ScaleComponentWeights.FromGrossTareTons(10m, 3m).AllValid.ShouldBeTrue();
        ScaleComponentWeights.FromGrossTareTons(10m, 3m).NetTon.ShouldBe(7m);
    }
}
