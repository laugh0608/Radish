using ComToHtri.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace ComToHtri.Permissions;

public class ComToHtriPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(ComToHtriPermissions.GroupName, L("Permission:ComToHtri"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<ComToHtriResource>(name);
    }
}
