using Microsoft.Extensions.Localization;
using Radish.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace Radish;

[Dependency(ReplaceServices = true)]
public class RadishBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<RadishResource> _localizer;

    public RadishBrandingProvider(IStringLocalizer<RadishResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
