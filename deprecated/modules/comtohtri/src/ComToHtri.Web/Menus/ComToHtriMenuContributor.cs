using System.Threading.Tasks;
using Volo.Abp.UI.Navigation;

namespace ComToHtri.Web.Menus;

public class ComToHtriMenuContributor : IMenuContributor
{
    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            await ConfigureMainMenuAsync(context);
        }
    }

    private Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        //Add main menu items.
        context.Menu.AddItem(new ApplicationMenuItem(ComToHtriMenus.Prefix, displayName: "ComToHtri", "~/ComToHtri", icon: "fa fa-globe"));

        return Task.CompletedTask;
    }
}
