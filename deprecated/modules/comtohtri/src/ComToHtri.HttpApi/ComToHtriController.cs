using ComToHtri.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace ComToHtri;

public abstract class ComToHtriController : AbpControllerBase
{
    protected ComToHtriController()
    {
        LocalizationResource = typeof(ComToHtriResource);
    }
}
