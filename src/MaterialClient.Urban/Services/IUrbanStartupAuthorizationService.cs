namespace MaterialClient.Urban.Services;

/// <summary>
///     Holds startup authorization outcome from <see cref="MaterialClientUrbanModule"/> for App layer branching.
/// </summary>
public interface IUrbanStartupAuthorizationService
{
    UrbanStartupAuthorizationResult Result { get; }

    bool IsAuthorized { get; }

    void SetResult(UrbanStartupAuthorizationResult result);
}
