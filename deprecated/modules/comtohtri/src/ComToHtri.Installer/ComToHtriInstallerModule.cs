using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace ComToHtri;

[DependsOn(
    typeof(AbpVirtualFileSystemModule)
    )]
public class ComToHtriInstallerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<ComToHtriInstallerModule>();
        });
    }
}
