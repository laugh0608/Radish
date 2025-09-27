using Volo.Abp.Modularity;

namespace Radish;

public abstract class RadishApplicationTestBase<TStartupModule> : RadishTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
