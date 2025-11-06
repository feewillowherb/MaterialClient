using Volo.Abp.Modularity;

namespace MaterialClient.Common;

/* Inherit from this class for your domain layer tests. */
public abstract class MaterialClientDomainTestBase<TStartupModule> : MaterialClientTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
}

