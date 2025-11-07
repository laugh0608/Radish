using Volo.Abp.Modularity;

namespace ComToHtri;

[DependsOn(
    typeof(ComToHtriDomainModule),
    typeof(ComToHtriTestBaseModule)
)]
public class ComToHtriDomainTestModule : AbpModule
{

}
