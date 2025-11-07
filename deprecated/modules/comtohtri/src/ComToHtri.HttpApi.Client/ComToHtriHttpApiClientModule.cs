using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace ComToHtri;

[DependsOn(
    typeof(ComToHtriApplicationContractsModule),
    typeof(AbpHttpClientModule))]
public class ComToHtriHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(ComToHtriApplicationContractsModule).Assembly,
            ComToHtriRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<ComToHtriHttpApiClientModule>();
        });

    }
}
