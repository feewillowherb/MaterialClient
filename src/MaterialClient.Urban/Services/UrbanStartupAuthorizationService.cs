using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Services;

public class UrbanStartupAuthorizationService : IUrbanStartupAuthorizationService, ISingletonDependency
{
    public UrbanStartupAuthorizationResult Result { get; private set; } =
        new(false, "Startup authorization has not been evaluated.", null);

    public bool IsAuthorized => Result.IsAuthorized;

    public void SetResult(UrbanStartupAuthorizationResult result) => Result = result;
}
