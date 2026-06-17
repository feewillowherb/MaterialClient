namespace MaterialClient.Urban.Services;

/// <summary>
///     Result of startup JWT authorization evaluated during ABP module initialization.
/// </summary>
public record UrbanStartupAuthorizationResult(
    bool IsAuthorized,
    string FailureMessage,
    Guid? ProId);
