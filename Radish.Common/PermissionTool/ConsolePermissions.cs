using Radish.Common.HttpContextTool;

namespace Radish.Common.PermissionTool;

public static class ConsolePermissions
{
    public const string DashboardView = "console.dashboard.view";
    public const string ApplicationsView = "console.applications.view";
    public const string ProductsView = "console.products.view";
    public const string OrdersView = "console.orders.view";
    public const string UsersView = "console.users.view";
    public const string RolesView = "console.roles.view";
    public const string RolesCreate = "console.roles.create";
    public const string RolesEdit = "console.roles.edit";
    public const string RolesToggle = "console.roles.toggle";
    public const string RolesDelete = "console.roles.delete";
    public const string TagsView = "console.tags.view";
    public const string StickersView = "console.stickers.view";
    public const string SystemConfigView = "console.system-config.view";
    public const string HangfireView = "console.hangfire.view";

    private static readonly IReadOnlyDictionary<string, string[]> ApiPermissionMappings =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["/api/v1/Role/GetRoleList"] = new[] { RolesView },
            ["/api/v1/Role/GetRoleById"] = new[] { RolesView, RolesEdit },
            ["/api/v1/Role/CreateRole"] = new[] { RolesCreate },
            ["/api/v1/Role/UpdateRole"] = new[] { RolesEdit },
            ["/api/v1/Role/DeleteRole"] = new[] { RolesDelete },
            ["/api/v1/Role/ToggleRoleStatus"] = new[] { RolesToggle },
        };

    private static readonly string[] AdminDefaultPermissions =
    {
        DashboardView,
        ApplicationsView,
        ProductsView,
        OrdersView,
        UsersView,
        RolesView,
        RolesCreate,
        RolesEdit,
        RolesToggle,
        RolesDelete,
        TagsView,
        StickersView,
        SystemConfigView,
        HangfireView,
    };

    public static IReadOnlyCollection<string> GetDefaultPermissions(IReadOnlyCollection<string> roleNames)
    {
        if (roleNames.Count <= 0)
        {
            return Array.Empty<string>();
        }

        if (roleNames.Contains(UserRoles.System, StringComparer.OrdinalIgnoreCase) ||
            roleNames.Contains(UserRoles.Admin, StringComparer.OrdinalIgnoreCase))
        {
            return AdminDefaultPermissions;
        }

        return Array.Empty<string>();
    }

    public static IReadOnlyCollection<string> GetPermissionsByApiUrl(string? apiUrl)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            return Array.Empty<string>();
        }

        return ApiPermissionMappings.TryGetValue(apiUrl.Trim(), out var permissions)
            ? permissions
            : Array.Empty<string>();
    }
}
