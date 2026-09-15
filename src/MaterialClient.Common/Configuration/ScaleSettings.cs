using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.Common.Configuration;

/// <summary>
///     Scale settings configuration
/// </summary>
public class ScaleSettings
{
    /// <summary>
    ///     Serial port (e.g., COM3)
    /// </summary>
    public string SerialPort { get; set; } = "COM3";

    /// <summary>
    ///     Baud rate (e.g., 9600)
    /// </summary>
    public string BaudRate { get; set; } = "9600";

    /// <summary>
    ///     Transmission format type (default: continuous tF0).
    /// </summary>
    public TransmissionFormatType TransmissionFormatType { get; set; } =
        TransmissionFormatType.TransmissionFormatType0;

    /// <summary>
    ///     Opaque communication parameter shared across formats.
    ///     Protocol implementers own parsing; facade must not centrally parse.
    ///     Yaohua default after reset: <c>A</c>.
    /// </summary>
    public string? CommunicationParameter { get; set; }

    /// <summary>
    ///     Scale unit (default: Kg)
    /// </summary>
    public ScaleUnit ScaleUnit { get; set; } = ScaleUnit.Kg;

    /// <summary>
    ///     Scale type (default: Yaohua)
    /// </summary>
    public ScaleType ScaleType { get; set; } = ScaleType.Yaohua;

    /// <summary>
    ///     Returns true when serial port and baud rate are configured.
    /// </summary>
    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(SerialPort) &&
        !string.IsNullOrWhiteSpace(BaudRate);

    /// <summary>
    ///     Resets <see cref="CommunicationParameter"/> to the default for
    ///     current <see cref="ScaleType"/> and <see cref="TransmissionFormatType"/>.
    /// </summary>
    public void ResetCommunicationParameterToDefault()
    {
        CommunicationParameter = DefaultCommunicationParameter(ScaleType, TransmissionFormatType);
    }

    /// <summary>
    ///     Applies a new transmission format and resets the communication parameter.
    /// </summary>
    public void ApplyTransmissionFormatChange(TransmissionFormatType newFormat)
    {
        TransmissionFormatType = newFormat;
        ResetCommunicationParameterToDefault();
    }

    public static string? DefaultCommunicationParameter(
        ScaleType scaleType,
        TransmissionFormatType format) =>
        scaleType == ScaleType.Yaohua ? "A" : null;
}
