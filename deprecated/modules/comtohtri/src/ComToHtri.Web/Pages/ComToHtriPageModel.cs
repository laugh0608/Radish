using ComToHtri.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace ComToHtri.Web.Pages;

/* Inherit your PageModel classes from this class.
 */
public abstract class ComToHtriPageModel : AbpPageModel
{
    protected ComToHtriPageModel()
    {
        LocalizationResourceType = typeof(ComToHtriResource);
        ObjectMapperContext = typeof(ComToHtriWebModule);
    }
}
