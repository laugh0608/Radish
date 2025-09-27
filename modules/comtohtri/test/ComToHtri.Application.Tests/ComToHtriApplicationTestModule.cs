using Volo.Abp.Modularity;

namespace ComToHtri;

[DependsOn(
    typeof(ComToHtriApplicationModule),
    typeof(ComToHtriDomainTestModule)
    )]
public class ComToHtriApplicationTestModule : AbpModule
{

}
