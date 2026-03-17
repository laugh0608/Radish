using Radish.Common.HttpContextTool;

namespace Radish.Common.PermissionTool;

public static class ConsolePermissions
{
    public const string Access = "console.access";
    public const string DashboardView = "console.dashboard.view";
    public const string ApplicationsView = "console.applications.view";
    public const string ApplicationsCreate = "console.applications.create";
    public const string ApplicationsEdit = "console.applications.edit";
    public const string ApplicationsDelete = "console.applications.delete";
    public const string ApplicationsResetSecret = "console.applications.reset-secret";
    public const string ProductsView = "console.products.view";
    public const string ProductsCreate = "console.products.create";
    public const string ProductsEdit = "console.products.edit";
    public const string ProductsDelete = "console.products.delete";
    public const string ProductsToggleSale = "console.products.toggle-sale";
    public const string OrdersView = "console.orders.view";
    public const string OrdersRetry = "console.orders.retry";
    public const string UsersView = "console.users.view";
    public const string RolesView = "console.roles.view";
    public const string RolesCreate = "console.roles.create";
    public const string RolesEdit = "console.roles.edit";
    public const string RolesToggle = "console.roles.toggle";
    public const string RolesDelete = "console.roles.delete";
    public const string TagsView = "console.tags.view";
    public const string TagsCreate = "console.tags.create";
    public const string TagsEdit = "console.tags.edit";
    public const string TagsDelete = "console.tags.delete";
    public const string TagsRestore = "console.tags.restore";
    public const string TagsToggle = "console.tags.toggle";
    public const string TagsSort = "console.tags.sort";
    public const string StickersView = "console.stickers.view";
    public const string StickersCreate = "console.stickers.create";
    public const string StickersEdit = "console.stickers.edit";
    public const string StickersDelete = "console.stickers.delete";
    public const string StickersToggle = "console.stickers.toggle";
    public const string StickersSort = "console.stickers.sort";
    public const string StickersBatchUpload = "console.stickers.batch-upload";
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
            ["/api/v1/Shop/GetCategories"] = new[] { ProductsView },
            ["/api/v1/Shop/AdminGetProducts"] = new[] { ProductsView },
            ["/api/v1/Shop/CreateProduct"] = new[] { ProductsCreate },
            ["/api/v1/Shop/UpdateProduct"] = new[] { ProductsEdit },
            ["/api/v1/Shop/DeleteProduct/.+"] = new[] { ProductsDelete },
            ["/api/v1/Shop/PutOnSale/.+"] = new[] { ProductsToggleSale },
            ["/api/v1/Shop/TakeOffSale/.+"] = new[] { ProductsToggleSale },
            ["/api/v1/Shop/AdminGetOrders"] = new[] { OrdersView },
            ["/api/v1/Shop/RetryGrantBenefit/.+"] = new[] { OrdersRetry },
            ["/api/v1/Tag/GetPage"] = new[] { TagsView },
            ["/api/v1/Tag/Create"] = new[] { TagsCreate },
            ["/api/v1/Tag/Update/.+"] = new[] { TagsEdit },
            ["/api/v1/Tag/Delete/.+"] = new[] { TagsDelete },
            ["/api/v1/Tag/Restore/.+"] = new[] { TagsRestore },
            ["/api/v1/Tag/ToggleStatus/.+"] = new[] { TagsToggle },
            ["/api/v1/Tag/UpdateSort/.+"] = new[] { TagsSort },
            ["/api/v1/Sticker/GetAdminGroups"] = new[] { StickersView },
            ["/api/v1/Sticker/CreateGroup"] = new[] { StickersCreate },
            ["/api/v1/Sticker/UpdateGroup/.+"] = new[] { StickersEdit, StickersToggle },
            ["/api/v1/Sticker/DeleteGroup/.+"] = new[] { StickersDelete },
            ["/api/v1/Sticker/CheckGroupCode"] = new[] { StickersCreate, StickersEdit },
            ["/api/v1/Sticker/CheckStickerCode"] = new[] { StickersCreate, StickersEdit },
            ["/api/v1/Sticker/NormalizeCode"] = new[] { StickersBatchUpload },
            ["/api/v1/Sticker/GetGroupStickers/.+"] = new[] { StickersView },
            ["/api/v1/Sticker/AddSticker"] = new[] { StickersCreate },
            ["/api/v1/Sticker/BatchAddStickers"] = new[] { StickersBatchUpload },
            ["/api/v1/Sticker/UpdateSticker/.+"] = new[] { StickersEdit },
            ["/api/v1/Sticker/DeleteSticker/.+"] = new[] { StickersDelete },
            ["/api/v1/Sticker/BatchUpdateSort"] = new[] { StickersSort },
            ["/api/v1/SystemConfig/GetSystemConfigs"] = new[] { SystemConfigView },
            ["/api/v1/SystemConfig/GetConfigCategories"] = new[] { SystemConfigView },
            ["/api/v1/SystemConfig/GetConfigById"] = new[] { SystemConfigView, SystemConfigEdit },
            ["/api/v1/SystemConfig/CreateConfig"] = new[] { SystemConfigCreate },
            ["/api/v1/SystemConfig/UpdateConfig"] = new[] { SystemConfigEdit },
            ["/api/v1/SystemConfig/DeleteConfig"] = new[] { SystemConfigDelete },
            ["/api/v1/Statistics/GetDashboardStats"] = new[] { DashboardView },
            ["/hangfire(/.*)?"] = new[] { HangfireView },
        };

    private static readonly string[] AdminDefaultPermissions =
    {
        Access,
        DashboardView,
        ApplicationsView,
        ApplicationsCreate,
        ApplicationsEdit,
        ApplicationsDelete,
        ApplicationsResetSecret,
        ProductsView,
        ProductsCreate,
        ProductsEdit,
        ProductsDelete,
        ProductsToggleSale,
        OrdersView,
        OrdersRetry,
        UsersView,
        RolesView,
        RolesCreate,
        RolesEdit,
        RolesToggle,
        RolesDelete,
        TagsView,
        TagsCreate,
        TagsEdit,
        TagsDelete,
        TagsRestore,
        TagsToggle,
        TagsSort,
        StickersView,
        StickersCreate,
        StickersEdit,
        StickersDelete,
        StickersToggle,
        StickersSort,
        StickersBatchUpload,
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
