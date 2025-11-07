using Radish.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace Radish.Permissions;

public class RadishPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(RadishPermissions.GroupName);

        var booksPermission = myGroup.AddPermission(RadishPermissions.Books.Default, L("Permission:Books"));
        booksPermission.AddChild(RadishPermissions.Books.Create, L("Permission:Books.Create"));
        booksPermission.AddChild(RadishPermissions.Books.Edit, L("Permission:Books.Edit"));
        booksPermission.AddChild(RadishPermissions.Books.Delete, L("Permission:Books.Delete"));
        //Define your own permissions here. Example:
        //myGroup.AddPermission(RadishPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<RadishResource>(name);
    }
}
