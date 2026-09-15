using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Services.TruckScale.Protocols;

/// <summary>
///     Thrown when ScaleType x TransmissionFormatType is unsupported or not yet implemented.
/// </summary>
public sealed class UnsupportedTransmissionFormatException : Exception
{
    public ScaleType ScaleType { get; }
    public TransmissionFormatType TransmissionFormatType { get; }

    public UnsupportedTransmissionFormatException(
        ScaleType scaleType,
        TransmissionFormatType transmissionFormatType,
        string? message = null)
        : base(message ??
               $"Transmission format {transmissionFormatType} is not supported for scale type {scaleType}.")
    {
        ScaleType = scaleType;
        TransmissionFormatType = transmissionFormatType;
    }
}
