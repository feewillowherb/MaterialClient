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
    ///     Communication method (e.g., TF0)
    /// </summary>
    public string CommunicationMethod { get; set; } = "TF0";

    /// <summary>
    ///     Scale unit (default: Ton)
    /// </summary>
    public ScaleUnit ScaleUnit { get; set; } = ScaleUnit.Kg;

    /// <summary>
    ///     判断配置是否有效
    ///     需要SerialPort、BaudRate和CommunicationMethod都不为空
    /// </summary>
    /// <returns>如果配置有效返回true，否则返回false</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(SerialPort) &&
               !string.IsNullOrWhiteSpace(BaudRate) &&
               !string.IsNullOrWhiteSpace(CommunicationMethod);
    }
}