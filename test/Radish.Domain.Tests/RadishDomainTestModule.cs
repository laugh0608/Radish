using Volo.Abp.Modularity;

namespace Radish;

[DependsOn(
    typeof(RadishDomainModule),
    typeof(RadishTestBaseModule)
)]
public class RadishDomainTestModule : AbpModule
{

}
