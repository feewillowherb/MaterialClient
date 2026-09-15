using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Services.TruckScale.Protocols;

/// <summary>
///     ScaleType x TransmissionFormatType protocol handler.
/// </summary>
public interface IScaleTransmissionProtocol
{
    bool Supports(ScaleType scaleType, TransmissionFormatType format);

    /// <summary>
    ///     Throws when the pair is not supported or not implemented.
    /// </summary>
    void EnsureSupported(ScaleType scaleType, TransmissionFormatType format);

    void OnStart(ScaleProtocolContext context);

    void OnDataReceived(ScaleProtocolContext context);

    void OnStop();
}
