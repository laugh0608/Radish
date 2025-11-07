using Localization.Resources.AbpUi;
using ComToHtri.Localization;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace ComToHtri;

[DependsOn(
    typeof(ComToHtriApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule))]
public class ComToHtriHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(ComToHtriHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<ComToHtriResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });
    }
}
