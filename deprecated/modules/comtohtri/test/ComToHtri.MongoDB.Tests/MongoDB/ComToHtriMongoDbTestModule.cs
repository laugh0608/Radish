using System;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace ComToHtri.MongoDB;

[DependsOn(
    typeof(ComToHtriApplicationTestModule),
    typeof(ComToHtriMongoDbModule)
)]
public class ComToHtriMongoDbTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default = MongoDbFixture.GetRandomConnectionString();
        });
    }
}
