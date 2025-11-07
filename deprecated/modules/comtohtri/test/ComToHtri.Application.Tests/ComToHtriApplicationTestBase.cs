using Volo.Abp.Modularity;

namespace ComToHtri;

/* Inherit from this class for your application layer tests.
 * See SampleAppService_Tests for example.
 */
public abstract class ComToHtriApplicationTestBase<TStartupModule> : ComToHtriTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
