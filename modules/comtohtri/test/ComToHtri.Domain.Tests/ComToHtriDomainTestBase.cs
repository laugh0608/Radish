using Volo.Abp.Modularity;

namespace ComToHtri;

/* Inherit from this class for your domain layer tests.
 * See SampleManager_Tests for example.
 */
public abstract class ComToHtriDomainTestBase<TStartupModule> : ComToHtriTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
