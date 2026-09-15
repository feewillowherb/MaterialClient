using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Services.TruckScale.Protocols.TestMode;

public sealed class TestModeTf0Protocol : IScaleTransmissionProtocol
{
    public bool Supports(ScaleType scaleType, TransmissionFormatType format) =>
        scaleType == ScaleType.TestMode &&
        format == TransmissionFormatType.TransmissionFormatType0;

    public void EnsureSupported(ScaleType scaleType, TransmissionFormatType format)
    {
        if (!Supports(scaleType, format))
            throw new UnsupportedTransmissionFormatException(scaleType, format);
    }

    public void OnStart(ScaleProtocolContext context)
    {
    }

    public void OnDataReceived(ScaleProtocolContext context)
    {
    }

    public void OnStop()
    {
    }
}
