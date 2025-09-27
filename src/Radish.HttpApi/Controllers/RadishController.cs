using Radish.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Radish.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class RadishController : AbpControllerBase
{
    protected RadishController()
    {
        LocalizationResource = typeof(RadishResource);
    }
}
