using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace ComToHtri;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(ComToHtriDomainSharedModule)
)]
public class ComToHtriDomainModule : AbpModule
{

}
