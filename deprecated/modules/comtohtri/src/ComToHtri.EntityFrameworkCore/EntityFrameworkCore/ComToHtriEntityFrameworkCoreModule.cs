using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace ComToHtri.EntityFrameworkCore;

[DependsOn(
    typeof(ComToHtriDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class ComToHtriEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<ComToHtriDbContext>(options =>
        {
            options.AddDefaultRepositories<IComToHtriDbContext>(includeAllEntities: true);
            
            /* Add custom repositories here. Example:
            * options.AddRepository<Question, EfCoreQuestionRepository>();
            */
        });
    }
}
