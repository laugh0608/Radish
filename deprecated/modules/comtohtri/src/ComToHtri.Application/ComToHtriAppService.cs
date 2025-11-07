using ComToHtri.Localization;
using Volo.Abp.Application.Services;

namespace ComToHtri;

public abstract class ComToHtriAppService : ApplicationService
{
    protected ComToHtriAppService()
    {
        LocalizationResource = typeof(ComToHtriResource);
        ObjectMapperContext = typeof(ComToHtriApplicationModule);
    }
}
