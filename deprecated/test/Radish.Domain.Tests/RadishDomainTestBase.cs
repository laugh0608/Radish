using Volo.Abp.Modularity;

namespace Radish;

/* Inherit from this class for your domain layer tests. */
public abstract class RadishDomainTestBase<TStartupModule> : RadishTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
