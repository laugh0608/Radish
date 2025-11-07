using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.MongoDB;

namespace ComToHtri.MongoDB;

[DependsOn(
    typeof(ComToHtriDomainModule),
    typeof(AbpMongoDbModule)
    )]
public class ComToHtriMongoDbModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMongoDbContext<ComToHtriMongoDbContext>(options =>
        {
            options.AddDefaultRepositories<IComToHtriMongoDbContext>();
            
            /* Add custom repositories here. Example:
             * options.AddRepository<Question, MongoQuestionRepository>();
             */
        });
    }
}
