using Radish.Common.HttpContextTool;

namespace Radish.Common.PermissionTool;

public static class ConsolePermissions
{
    public const string DashboardView = "console.dashboard.view";
    public const string ApplicationsView = "console.applications.view";
    public const string ApplicationsCreate = "console.applications.create";
    public const string ApplicationsEdit = "console.applications.edit";
    public const string ApplicationsDelete = "console.applications.delete";
    public const string ApplicationsResetSecret = "console.applications.reset-secret";
    public const string ProductsView = "console.products.view";
    public const string OrdersView = "console.orders.view";
    public const string UsersView = "console.users.view";
    public const string UsersCreate = "console.users.create";
    public const string UsersEdit = "console.users.edit";
    public const string UsersDelete = "console.users.delete";
    public const string UsersStatus = "console.users.status";
    public const string UsersResetPassword = "console.users.reset-password";
    public const string UsersForceLogout = "console.users.force-logout";
    public const string RolesView = "console.roles.view";
    public const string RolesCreate = "console.roles.create";
    public const string RolesEdit = "console.roles.edit";
    public const string RolesToggle = "console.roles.toggle";
    public const string RolesDelete = "console.roles.delete";
    public const string TagsView = "console.tags.view";
    public const string StickersView = "console.stickers.view";
    public const string SystemConfigView = "console.system-config.view";
    public const string SystemConfigCreate = "console.system-config.create";
    public const string SystemConfigEdit = "console.system-config.edit";
    public const string SystemConfigDelete = "console.system-config.delete";
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
            ["/api/v1/User/GetUserList"] = new[] { UsersView },
            ["/api/v1/User/GetUserById/\\d+"] = new[] { UsersView },
            ["/api/v1/Client/GetClients"] = new[] { ApplicationsView },
            ["/api/v1/Client/GetClient/.+"] = new[] { ApplicationsView, ApplicationsEdit },
            ["/api/v1/Client/CreateClient"] = new[] { ApplicationsCreate },
            ["/api/v1/Client/UpdateClient/.+"] = new[] { ApplicationsEdit },
            ["/api/v1/Client/DeleteClient/.+"] = new[] { ApplicationsDelete },
            ["/api/v1/Client/ResetClientSecret/.+"] = new[] { ApplicationsResetSecret },
            ["/api/v1/SystemConfig/GetSystemConfigs"] = new[] { SystemConfigView },
            ["/api/v1/SystemConfig/GetConfigCategories"] = new[] { SystemConfigView },
            ["/api/v1/SystemConfig/GetConfigById"] = new[] { SystemConfigView, SystemConfigEdit },
            ["/api/v1/SystemConfig/CreateConfig"] = new[] { SystemConfigCreate },
            ["/api/v1/SystemConfig/UpdateConfig"] = new[] { SystemConfigEdit },
            ["/api/v1/SystemConfig/DeleteConfig"] = new[] { SystemConfigDelete },
        };

    private static readonly string[] AdminDefaultPermissions =
    {
        DashboardView,
        ApplicationsView,
        ApplicationsCreate,
        ApplicationsEdit,
        ApplicationsDelete,
        ApplicationsResetSecret,
        ProductsView,
        OrdersView,
        UsersView,
        UsersCreate,
        UsersEdit,
        UsersDelete,
        UsersStatus,
        UsersResetPassword,
        UsersForceLogout,
        RolesView,
        RolesCreate,
        RolesEdit,
        RolesToggle,
        RolesDelete,
        TagsView,
        StickersView,
        SystemConfigView,
        SystemConfigCreate,
        SystemConfigEdit,
        SystemConfigDelete,
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
