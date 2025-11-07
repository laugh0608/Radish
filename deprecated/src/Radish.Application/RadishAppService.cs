using Radish.Localization;
using Volo.Abp.Application.Services;

namespace Radish;

/* Inherit your application services from this class.
 */
public abstract class RadishAppService : ApplicationService
{
    protected RadishAppService()
    {
        LocalizationResource = typeof(RadishResource);
    }
}
