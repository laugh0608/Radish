using Volo.Abp.Modularity;

namespace Radish;

[DependsOn(
    typeof(RadishApplicationModule),
    typeof(RadishDomainTestModule)
)]
public class RadishApplicationTestModule : AbpModule
{

}
