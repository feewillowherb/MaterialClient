namespace MaterialClient.Common.Configuration;

/// <summary>
///     SignalR client configuration options.
/// </summary>
public class SignalRClientOptions
{
    /// <summary>
    ///     SignalR server URL.
    ///     Default: http://localhost:5000/hubs/devicestatus
    /// </summary>
    public string ServerUrl { get; set; } = "http://localhost:5000/hubs/devicestatus";

    /// <summary>
    ///     Reconnect delay intervals in seconds.
    ///     Default: [0, 2, 10, 30] (exponential backoff).
    /// </summary>
    public int[] ReconnectDelays { get; set; } = [0, 2, 10, 30];

    /// <summary>
    ///     Maximum reconnect attempts before giving up.
    ///     Default: 10.
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 10;

    /// <summary>
    ///     Maximum number of messages to queue when disconnected.
    ///     Default: 100. Must be &gt; 0 and &lt;= 1000.
    /// </summary>
    public int MessageQueueSize { get; set; } = 100;

    /// <summary>
    ///     JWT token for authentication. If empty, connects without auth.
    /// </summary>
    public string? AccessToken { get; set; }
}
