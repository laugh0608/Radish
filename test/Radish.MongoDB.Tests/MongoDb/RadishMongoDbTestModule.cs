using System;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace Radish.MongoDB;

[DependsOn(
    typeof(RadishApplicationTestModule),
    typeof(RadishMongoDbModule)
)]
public class RadishMongoDbTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default = RadishMongoDbFixture.GetRandomConnectionString();
        });
    }
}
