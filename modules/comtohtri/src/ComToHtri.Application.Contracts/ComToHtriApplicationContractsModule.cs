using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Volo.Abp.Authorization;

namespace ComToHtri;

[DependsOn(
    typeof(ComToHtriDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class ComToHtriApplicationContractsModule : AbpModule
{

}
