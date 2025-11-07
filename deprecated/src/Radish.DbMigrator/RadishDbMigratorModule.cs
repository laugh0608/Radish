using Radish.MongoDB;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace Radish.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(RadishMongoDbModule),
    typeof(RadishApplicationContractsModule)
)]
public class RadishDbMigratorModule : AbpModule
{
}
