namespace MaterialClient.Common.Events;

/// <summary>
///     Token refresh event (published via ABP ILocalEventBus when API returns 401 Unauthorized)
/// </summary>
public class SessionRefreshRequiredEto(string apiEndpoint, int statusCode, DateTime occurredAtUtc)
{
    /// <summary>
    ///     The API endpoint that returned 401
    /// </summary>
    public string ApiEndpoint { get; } = apiEndpoint;

    /// <summary>
    ///     HTTP status code (expected to be 401)
    /// </summary>
    public int StatusCode { get; } = statusCode;

    /// <summary>
    ///     UTC timestamp when the 401 was detected
    /// </summary>
    public DateTime OccurredAtUtc { get; } = occurredAtUtc;
}
