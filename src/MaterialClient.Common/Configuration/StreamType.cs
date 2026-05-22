namespace MaterialClient.Common.Configuration;

/// <summary>
///     Stream type for camera capture
/// </summary>
public enum StreamType
{
    /// <summary>
    ///     子码流 (Substream) - Lower quality, faster capture
    /// </summary>
    Substream = 0,

    /// <summary>
    ///     主码流 (Mainstream) - Higher quality, slower capture
    /// </summary>
    Mainstream = 1
}

