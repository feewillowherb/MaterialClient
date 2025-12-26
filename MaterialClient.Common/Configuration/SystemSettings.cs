namespace MaterialClient.Common.Configuration;

/// <summary>
///     System settings configuration
/// </summary>
public class SystemSettings
{
    /// <summary>
    ///     Enable auto-start on boot
    /// </summary>
    public bool EnableAutoStart { get; set; } = false;

    /// <summary>
    ///     Capture stream type (Substream or Mainstream)
    /// </summary>
    public StreamType CaptureStreamType { get; set; } = StreamType.Substream;
}