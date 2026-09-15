using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Services.TruckScale.Protocols.Unsupported;

/// <summary>
///     Shared handler for unsupported / not-yet-implemented transmission formats (including Yaohua tf1 in Phase 1).
/// </summary>
public sealed class UnsupportedTransmissionFormatProtocol : IScaleTransmissionProtocol
{
    public bool Supports(ScaleType scaleType, TransmissionFormatType format) => false;

    public void EnsureSupported(ScaleType scaleType, TransmissionFormatType format) =>
        throw new UnsupportedTransmissionFormatException(scaleType, format);

    public void OnStart(ScaleProtocolContext context) =>
        EnsureSupported(context.Settings.ScaleType, context.Settings.TransmissionFormatType);

    public void OnDataReceived(ScaleProtocolContext context) =>
        EnsureSupported(context.Settings.ScaleType, context.Settings.TransmissionFormatType);

    public void OnStop()
    {
    }
}
