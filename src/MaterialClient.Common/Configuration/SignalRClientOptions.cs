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
    ///     Maximum reconnect attempts in one retry window.
    ///     Default: 10.
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 10;

    /// <summary>
    ///     When true, after a retry window is exhausted wait <see cref="ReconnectResetHours"/>
    ///     then reset the attempt counter and try again (covers long server outages).
    ///     When false, stop after <see cref="MaxReconnectAttempts"/>.
    ///     Default: false.
    /// </summary>
    public bool PersistentReconnect { get; set; }

    /// <summary>
    ///     Hours to wait before resetting the reconnect attempt window when
    ///     <see cref="PersistentReconnect"/> is true. Default: 12.
    /// </summary>
    public int ReconnectResetHours { get; set; } = 12;

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
