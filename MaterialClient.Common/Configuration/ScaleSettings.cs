namespace MaterialClient.Common.Configuration;

/// <summary>
/// Scale settings configuration
/// </summary>
public class ScaleSettings
{
    /// <summary>
    /// Serial port (e.g., COM3)
    /// </summary>
    public string SerialPort { get; set; } = "COM3";

    /// <summary>
    /// Baud rate (e.g., 9600)
    /// </summary>
    public string BaudRate { get; set; } = "9600";

    /// <summary>
    /// Communication method (e.g., TF0)
    /// </summary>
    public string CommunicationMethod { get; set; } = "TF0";
}
