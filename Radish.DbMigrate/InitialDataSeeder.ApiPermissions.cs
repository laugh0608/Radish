using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

internal static partial class InitialDataSeeder
{
    /// <summary>初始化角色-API 权限（示例：允许 System/Admin 访问用户基本信息接口）</summary>
    private static async Task SeedPermissionsAsync(ISqlSugarClient db)
    {
        // 为当前用户与角色管理主链路建立 ApiModule 与 RoleModulePermission
        // 便于通过 RadishAuthPolicy 与 Console 权限快照进行验证。
        await RetireObsoleteContentModerationApiModulesAsync(db);

        var apiModules = new[]
        {
            new
            {
                ApiModuleId = 50000L,
                ApiModuleName = "Get current user by HttpContext",
                LinkUrl = "/api/v1/User/GetUserByHttpContext",
                ControllerName = "User",
                ActionName = "GetUserByHttpContext",
                Roles = new[] { 10000L, 10001L, 10002L }
            },
            new
            {
                ApiModuleId = 50010L,
                ApiModuleName = "Get role list",
                LinkUrl = "/api/v1/Role/GetRoleList",
                ControllerName = "Role",
                ActionName = "GetRoleList",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50011L,
                ApiModuleName = "Get role by id",
                LinkUrl = "/api/v1/Role/GetRoleById",
                ControllerName = "Role",
                ActionName = "GetRoleById",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50012L,
                ApiModuleName = "Create role",
                LinkUrl = "/api/v1/Role/CreateRole",
                ControllerName = "Role",
                ActionName = "CreateRole",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50013L,
                ApiModuleName = "Update role",
                LinkUrl = "/api/v1/Role/UpdateRole",
                ControllerName = "Role",
                ActionName = "UpdateRole",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50014L,
                ApiModuleName = "Delete role",
                LinkUrl = "/api/v1/Role/DeleteRole",
                ControllerName = "Role",
                ActionName = "DeleteRole",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50015L,
                ApiModuleName = "Toggle role status",
                LinkUrl = "/api/v1/Role/ToggleRoleStatus",
                ControllerName = "Role",
                ActionName = "ToggleRoleStatus",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50016L,
                ApiModuleName = "Get console resource tree",
                LinkUrl = "/api/v1/ConsoleAuthorization/GetResourceTree",
                ControllerName = "ConsoleAuthorization",
                ActionName = "GetResourceTree",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50017L,
                ApiModuleName = "Get role console authorization",
                LinkUrl = "/api/v1/ConsoleAuthorization/GetRoleAuthorization",
                ControllerName = "ConsoleAuthorization",
                ActionName = "GetRoleAuthorization",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50018L,
                ApiModuleName = "Get role permission preview",
                LinkUrl = "/api/v1/ConsoleAuthorization/GetRolePermissionPreview",
                ControllerName = "ConsoleAuthorization",
                ActionName = "GetRolePermissionPreview",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50019L,
                ApiModuleName = "Save role console authorization",
                LinkUrl = "/api/v1/ConsoleAuthorization/SaveRoleAuthorization",
                ControllerName = "ConsoleAuthorization",
                ActionName = "SaveRoleAuthorization",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50020L,
                ApiModuleName = "Get user list",
                LinkUrl = "/api/v1/User/GetUserList",
                ControllerName = "User",
                ActionName = "GetUserList",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50021L,
                ApiModuleName = "Get user by id",
                LinkUrl = "/api/v1/User/GetUserById/\\d+",
                ControllerName = "User",
                ActionName = "GetUserById",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50030L,
                ApiModuleName = "Get clients",
                LinkUrl = "/api/v1/Client/GetClients",
                ControllerName = "Client",
                ActionName = "GetClients",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50031L,
                ApiModuleName = "Get client",
                LinkUrl = "/api/v1/Client/GetClient/.+",
                ControllerName = "Client",
                ActionName = "GetClient",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50032L,
                ApiModuleName = "Create client",
                LinkUrl = "/api/v1/Client/CreateClient",
                ControllerName = "Client",
                ActionName = "CreateClient",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50033L,
                ApiModuleName = "Update client",
                LinkUrl = "/api/v1/Client/UpdateClient/.+",
                ControllerName = "Client",
                ActionName = "UpdateClient",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50034L,
                ApiModuleName = "Delete client",
                LinkUrl = "/api/v1/Client/DeleteClient/.+",
                ControllerName = "Client",
                ActionName = "DeleteClient",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50035L,
                ApiModuleName = "Reset client secret",
                LinkUrl = "/api/v1/Client/ResetClientSecret/.+",
                ControllerName = "Client",
                ActionName = "ResetClientSecret",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50040L,
                ApiModuleName = "Get system configs",
                LinkUrl = "/api/v1/SystemConfig/GetSystemConfigs",
                ControllerName = "SystemConfig",
                ActionName = "GetSystemConfigs",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50041L,
                ApiModuleName = "Get system config categories",
                LinkUrl = "/api/v1/SystemConfig/GetConfigCategories",
                ControllerName = "SystemConfig",
                ActionName = "GetConfigCategories",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50042L,
                ApiModuleName = "Get system config by id",
                LinkUrl = "/api/v1/SystemConfig/GetConfigById",
                ControllerName = "SystemConfig",
                ActionName = "GetConfigById",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50043L,
                ApiModuleName = "Create system config",
                LinkUrl = "/api/v1/SystemConfig/CreateConfig",
                ControllerName = "SystemConfig",
                ActionName = "CreateConfig",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50044L,
                ApiModuleName = "Update system config",
                LinkUrl = "/api/v1/SystemConfig/UpdateConfig",
                ControllerName = "SystemConfig",
                ActionName = "UpdateConfig",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50045L,
                ApiModuleName = "Delete system config",
                LinkUrl = "/api/v1/SystemConfig/DeleteConfig",
                ControllerName = "SystemConfig",
                ActionName = "DeleteConfig",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50048L,
                ApiModuleName = "Restore system config default",
                LinkUrl = "/api/v1/SystemConfig/RestoreConfigDefault",
                ControllerName = "SystemConfig",
                ActionName = "RestoreConfigDefault",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50046L,
                ApiModuleName = "Get dashboard stats",
                LinkUrl = "/api/v1/Statistics/GetDashboardStats",
                ControllerName = "Statistics",
                ActionName = "GetDashboardStats",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 500461L,
                ApiModuleName = "Get order trend stats",
                LinkUrl = "/api/v1/Statistics/GetOrderTrend",
                ControllerName = "Statistics",
                ActionName = "GetOrderTrend",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 500462L,
                ApiModuleName = "Get product sales ranking stats",
                LinkUrl = "/api/v1/Statistics/GetProductSalesRanking",
                ControllerName = "Statistics",
                ActionName = "GetProductSalesRanking",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 500463L,
                ApiModuleName = "Get user level distribution stats",
                LinkUrl = "/api/v1/Statistics/GetUserLevelDistribution",
                ControllerName = "Statistics",
                ActionName = "GetUserLevelDistribution",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50047L,
                ApiModuleName = "Hangfire dashboard",
                LinkUrl = "/hangfire(/.*)?",
                ControllerName = "Hangfire",
                ActionName = "Dashboard",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50179L,
                ApiModuleName = "Create Hangfire dashboard session",
                LinkUrl = "/api/v1/HangfireSession/Create",
                ControllerName = "HangfireSession",
                ActionName = "Create",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 500464L,
                ApiModuleName = "Get reliable task dead letters",
                LinkUrl = "/api/v1/ReliableOutbox/GetDeadLetters",
                ControllerName = "ReliableOutbox",
                ActionName = "GetDeadLetters",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 500465L,
                ApiModuleName = "Replay reliable task",
                LinkUrl = "/api/v1/ReliableOutbox/Replay",
                ControllerName = "ReliableOutbox",
                ActionName = "Replay",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50049L,
                ApiModuleName = "Get product categories",
                LinkUrl = "/api/v1/Shop/GetCategories",
                ControllerName = "Shop",
                ActionName = "GetCategories",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50050L,
                ApiModuleName = "Admin get products",
                LinkUrl = "/api/v1/Shop/AdminGetProducts",
                ControllerName = "Shop",
                ActionName = "AdminGetProducts",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 500501L,
                ApiModuleName = "Admin get product detail",
                LinkUrl = "/api/v1/Shop/AdminGetProduct/\\d+",
                ControllerName = "Shop",
                ActionName = "AdminGetProduct",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50051L,
                ApiModuleName = "Create product",
                LinkUrl = "/api/v1/Shop/CreateProduct",
                ControllerName = "Shop",
                ActionName = "CreateProduct",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50052L,
                ApiModuleName = "Update product",
                LinkUrl = "/api/v1/Shop/UpdateProduct",
                ControllerName = "Shop",
                ActionName = "UpdateProduct",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50053L,
                ApiModuleName = "Delete product",
                LinkUrl = "/api/v1/Shop/DeleteProduct/.+",
                ControllerName = "Shop",
                ActionName = "DeleteProduct",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50054L,
                ApiModuleName = "Put product on sale",
                LinkUrl = "/api/v1/Shop/PutOnSale/.+",
                ControllerName = "Shop",
                ActionName = "PutOnSale",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50055L,
                ApiModuleName = "Take product off sale",
                LinkUrl = "/api/v1/Shop/TakeOffSale/.+",
                ControllerName = "Shop",
                ActionName = "TakeOffSale",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50056L,
                ApiModuleName = "Admin get orders",
                LinkUrl = "/api/v1/Shop/AdminGetOrders",
                ControllerName = "Shop",
                ActionName = "AdminGetOrders",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 500561L,
                ApiModuleName = "Admin get order detail",
                LinkUrl = "/api/v1/Shop/AdminGetOrder/.+",
                ControllerName = "Shop",
                ActionName = "AdminGetOrder",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 500562L,
                ApiModuleName = "Admin get entitlement operations",
                LinkUrl = "/api/v1/Shop/AdminGetEntitlementOperations",
                ControllerName = "Shop",
                ActionName = "AdminGetEntitlementOperations",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 500563L,
                ApiModuleName = "Admin get user benefits",
                LinkUrl = "/api/v1/Shop/AdminGetUserBenefits",
                ControllerName = "Shop",
                ActionName = "AdminGetUserBenefits",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 500564L,
                ApiModuleName = "Admin revoke benefit",
                LinkUrl = "/api/v1/Shop/AdminRevokeBenefit/.+",
                ControllerName = "Shop",
                ActionName = "AdminRevokeBenefit",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50057L,
                ApiModuleName = "Retry grant benefit",
                LinkUrl = "/api/v1/Shop/RetryGrantBenefit/.+",
                ControllerName = "Shop",
                ActionName = "RetryGrantBenefit",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50058L,
                ApiModuleName = "Admin remark order",
                LinkUrl = "/api/v1/Shop/AdminRemarkOrder/.+",
                ControllerName = "Shop",
                ActionName = "AdminRemarkOrder",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50060L,
                ApiModuleName = "Get tag page",
                LinkUrl = "/api/v1/Tag/GetPage",
                ControllerName = "Tag",
                ActionName = "GetPage",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50061L,
                ApiModuleName = "Create tag",
                LinkUrl = "/api/v1/Tag/Create",
                ControllerName = "Tag",
                ActionName = "Create",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50062L,
                ApiModuleName = "Update tag",
                LinkUrl = "/api/v1/Tag/Update/.+",
                ControllerName = "Tag",
                ActionName = "Update",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50063L,
                ApiModuleName = "Delete tag",
                LinkUrl = "/api/v1/Tag/Delete/.+",
                ControllerName = "Tag",
                ActionName = "Delete",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50064L,
                ApiModuleName = "Restore tag",
                LinkUrl = "/api/v1/Tag/Restore/.+",
                ControllerName = "Tag",
                ActionName = "Restore",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50065L,
                ApiModuleName = "Toggle tag status",
                LinkUrl = "/api/v1/Tag/ToggleStatus/.+",
                ControllerName = "Tag",
                ActionName = "ToggleStatus",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50066L,
                ApiModuleName = "Update tag sort",
                LinkUrl = "/api/v1/Tag/UpdateSort/.+",
                ControllerName = "Tag",
                ActionName = "UpdateSort",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50070L,
                ApiModuleName = "Get admin sticker groups",
                LinkUrl = "/api/v1/Sticker/GetAdminGroups",
                ControllerName = "Sticker",
                ActionName = "GetAdminGroups",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50071L,
                ApiModuleName = "Create sticker group",
                LinkUrl = "/api/v1/Sticker/CreateGroup",
                ControllerName = "Sticker",
                ActionName = "CreateGroup",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50072L,
                ApiModuleName = "Update sticker group",
                LinkUrl = "/api/v1/Sticker/UpdateGroup/.+",
                ControllerName = "Sticker",
                ActionName = "UpdateGroup",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50083L,
                ApiModuleName = "Update sticker group status",
                LinkUrl = "/api/v1/Sticker/UpdateGroupStatus/.+",
                ControllerName = "Sticker",
                ActionName = "UpdateGroupStatus",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50073L,
                ApiModuleName = "Delete sticker group",
                LinkUrl = "/api/v1/Sticker/DeleteGroup/.+",
                ControllerName = "Sticker",
                ActionName = "DeleteGroup",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50074L,
                ApiModuleName = "Get group stickers",
                LinkUrl = "/api/v1/Sticker/GetGroupStickers/.+",
                ControllerName = "Sticker",
                ActionName = "GetGroupStickers",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50075L,
                ApiModuleName = "Add sticker",
                LinkUrl = "/api/v1/Sticker/AddSticker",
                ControllerName = "Sticker",
                ActionName = "AddSticker",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50076L,
                ApiModuleName = "Batch add stickers",
                LinkUrl = "/api/v1/Sticker/BatchAddStickers",
                ControllerName = "Sticker",
                ActionName = "BatchAddStickers",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50077L,
                ApiModuleName = "Update sticker",
                LinkUrl = "/api/v1/Sticker/UpdateSticker/.+",
                ControllerName = "Sticker",
                ActionName = "UpdateSticker",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50078L,
                ApiModuleName = "Delete sticker",
                LinkUrl = "/api/v1/Sticker/DeleteSticker/.+",
                ControllerName = "Sticker",
                ActionName = "DeleteSticker",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50079L,
                ApiModuleName = "Batch update sticker sort",
                LinkUrl = "/api/v1/Sticker/BatchUpdateSort",
                ControllerName = "Sticker",
                ActionName = "BatchUpdateSort",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50080L,
                ApiModuleName = "Check sticker group code",
                LinkUrl = "/api/v1/Sticker/CheckGroupCode",
                ControllerName = "Sticker",
                ActionName = "CheckGroupCode",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50081L,
                ApiModuleName = "Check sticker code",
                LinkUrl = "/api/v1/Sticker/CheckStickerCode",
                ControllerName = "Sticker",
                ActionName = "CheckStickerCode",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50082L,
                ApiModuleName = "Normalize sticker code",
                LinkUrl = "/api/v1/Sticker/NormalizeCode",
                ControllerName = "Sticker",
                ActionName = "NormalizeCode",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50100L,
                ApiModuleName = "Get category page",
                LinkUrl = "/api/v1/Category/GetPage",
                ControllerName = "Category",
                ActionName = "GetPage",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50101L,
                ApiModuleName = "Create category",
                LinkUrl = "/api/v1/Category/Create",
                ControllerName = "Category",
                ActionName = "Create",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50102L,
                ApiModuleName = "Update category",
                LinkUrl = "/api/v1/Category/Update/.+",
                ControllerName = "Category",
                ActionName = "Update",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50103L,
                ApiModuleName = "Delete category",
                LinkUrl = "/api/v1/Category/Delete/.+",
                ControllerName = "Category",
                ActionName = "Delete",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50104L,
                ApiModuleName = "Restore category",
                LinkUrl = "/api/v1/Category/Restore/.+",
                ControllerName = "Category",
                ActionName = "Restore",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50105L,
                ApiModuleName = "Toggle category status",
                LinkUrl = "/api/v1/Category/ToggleStatus/.+",
                ControllerName = "Category",
                ActionName = "ToggleStatus",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50106L,
                ApiModuleName = "Update category sort",
                LinkUrl = "/api/v1/Category/UpdateSort/.+",
                ControllerName = "Category",
                ActionName = "UpdateSort",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50114L,
                ApiModuleName = "Get moderation case queue",
                LinkUrl = "/api/v1/ContentModeration/GetCaseQueue",
                ControllerName = "ContentModeration",
                ActionName = "GetCaseQueue",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50115L,
                ApiModuleName = "Get moderation case",
                LinkUrl = "/api/v1/ContentModeration/GetCase/.+",
                ControllerName = "ContentModeration",
                ActionName = "GetCase",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50116L,
                ApiModuleName = "Capture moderation evidence",
                LinkUrl = "/api/v1/ContentModeration/CaptureEvidence",
                ControllerName = "ContentModeration",
                ActionName = "CaptureEvidence",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50117L,
                ApiModuleName = "Review moderation case",
                LinkUrl = "/api/v1/ContentModeration/ReviewCase",
                ControllerName = "ContentModeration",
                ActionName = "ReviewCase",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50118L,
                ApiModuleName = "Apply moderation corrective action",
                LinkUrl = "/api/v1/ContentModeration/ApplyCorrectiveAction",
                ControllerName = "ContentModeration",
                ActionName = "ApplyCorrectiveAction",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50119L,
                ApiModuleName = "Get moderation case events",
                LinkUrl = "/api/v1/ContentModeration/GetCaseEvents",
                ControllerName = "ContentModeration",
                ActionName = "GetCaseEvents",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50167L,
                ApiModuleName = "Get moderation appeal queue",
                LinkUrl = "/api/v1/ContentModeration/GetAppealQueue",
                ControllerName = "ContentModeration",
                ActionName = "GetAppealQueue",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50168L,
                ApiModuleName = "Get moderation appeal",
                LinkUrl = "/api/v1/ContentModeration/GetAppeal/.+",
                ControllerName = "ContentModeration",
                ActionName = "GetAppeal",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50169L,
                ApiModuleName = "Get moderation appeal events",
                LinkUrl = "/api/v1/ContentModeration/GetAppealEvents",
                ControllerName = "ContentModeration",
                ActionName = "GetAppealEvents",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50170L,
                ApiModuleName = "Start moderation appeal review",
                LinkUrl = "/api/v1/ContentModeration/StartAppealReview",
                ControllerName = "ContentModeration",
                ActionName = "StartAppealReview",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50171L,
                ApiModuleName = "Capture moderation appeal evidence",
                LinkUrl = "/api/v1/ContentModeration/CaptureAppealEvidence",
                ControllerName = "ContentModeration",
                ActionName = "CaptureAppealEvidence",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50172L,
                ApiModuleName = "Review moderation appeal",
                LinkUrl = "/api/v1/ContentModeration/ReviewAppeal",
                ControllerName = "ContentModeration",
                ActionName = "ReviewAppeal",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50173L,
                ApiModuleName = "Execute moderation appeal relief",
                LinkUrl = "/api/v1/ContentModeration/ExecuteAppealRelief",
                ControllerName = "ContentModeration",
                ActionName = "ExecuteAppealRelief",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50120L,
                ApiModuleName = "Get coin balance by user id",
                LinkUrl = "/api/v1/Coin/GetBalanceByUserId",
                ControllerName = "Coin",
                ActionName = "GetBalanceByUserId",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 501205L,
                ApiModuleName = "Admin get coin transactions",
                LinkUrl = "/api/v1/Coin/AdminGetTransactions",
                ControllerName = "Coin",
                ActionName = "AdminGetTransactions",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50121L,
                ApiModuleName = "Admin adjust coin balance",
                LinkUrl = "/api/v1/Coin/AdminAdjustBalance",
                ControllerName = "Coin",
                ActionName = "AdminAdjustBalance",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50130L,
                ApiModuleName = "Get user experience",
                LinkUrl = "/api/v1/Experience/GetUserExperience/.+",
                ControllerName = "Experience",
                ActionName = "GetUserExperience",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50131L,
                ApiModuleName = "Get level configs",
                LinkUrl = "/api/v1/Experience/GetLevelConfigs",
                ControllerName = "Experience",
                ActionName = "GetLevelConfigs",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50132L,
                ApiModuleName = "Admin adjust experience",
                LinkUrl = "/api/v1/Experience/AdminAdjustExperience",
                ControllerName = "Experience",
                ActionName = "AdminAdjustExperience",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50133L,
                ApiModuleName = "Recalculate level configs",
                LinkUrl = "/api/v1/Experience/RecalculateLevelConfigs",
                ControllerName = "Experience",
                ActionName = "RecalculateLevelConfigs",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50134L,
                ApiModuleName = "Admin freeze experience",
                LinkUrl = "/api/v1/Experience/AdminFreezeExperience",
                ControllerName = "Experience",
                ActionName = "AdminFreezeExperience",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50135L,
                ApiModuleName = "Admin unfreeze experience",
                LinkUrl = "/api/v1/Experience/AdminUnfreezeExperience",
                ControllerName = "Experience",
                ActionName = "AdminUnfreezeExperience",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50136L,
                ApiModuleName = "Get user experience daily stats",
                LinkUrl = "/api/v1/Experience/GetUserDailyStats/.+",
                ControllerName = "Experience",
                ActionName = "GetUserDailyStats",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50137L,
                ApiModuleName = "Get user experience governance actions",
                LinkUrl = "/api/v1/Experience/GetUserGovernanceActions/.+",
                ControllerName = "Experience",
                ActionName = "GetUserGovernanceActions",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50138L,
                ApiModuleName = "Get user experience transactions",
                LinkUrl = "/api/v1/Experience/GetUserTransactions/.+",
                ControllerName = "Experience",
                ActionName = "GetUserTransactions",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50139L,
                ApiModuleName = "Record experience governance review",
                LinkUrl = "/api/v1/Experience/AdminRecordGovernanceReview",
                ControllerName = "Experience",
                ActionName = "AdminRecordGovernanceReview",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50150L,
                ApiModuleName = "Admin get wiki documents",
                LinkUrl = "/api/v1/Wiki/AdminGetList",
                ControllerName = "Wiki",
                ActionName = "AdminGetList",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50151L,
                ApiModuleName = "Admin get wiki tree",
                LinkUrl = "/api/v1/Wiki/AdminGetTree",
                ControllerName = "Wiki",
                ActionName = "AdminGetTree",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50152L,
                ApiModuleName = "Admin get wiki document detail",
                LinkUrl = "/api/v1/Wiki/AdminGetById/\\d+",
                ControllerName = "Wiki",
                ActionName = "AdminGetById",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50178L,
                ApiModuleName = "Admin get wiki governance history",
                LinkUrl = "/api/v1/Wiki/AdminGetGovernanceHistory/\\d+",
                ControllerName = "Wiki",
                ActionName = "AdminGetGovernanceHistory",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50153L,
                ApiModuleName = "Get wiki revision list",
                LinkUrl = "/api/v1/Wiki/GetRevisionList/\\d+",
                ControllerName = "Wiki",
                ActionName = "GetRevisionList",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50154L,
                ApiModuleName = "Get wiki revision detail",
                LinkUrl = "/api/v1/Wiki/GetRevisionDetail/\\d+",
                ControllerName = "Wiki",
                ActionName = "GetRevisionDetail",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50155L,
                ApiModuleName = "Publish wiki document",
                LinkUrl = "/api/v1/Wiki/Publish/\\d+",
                ControllerName = "Wiki",
                ActionName = "Publish",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50156L,
                ApiModuleName = "Unpublish wiki document",
                LinkUrl = "/api/v1/Wiki/Unpublish/\\d+",
                ControllerName = "Wiki",
                ActionName = "Unpublish",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50157L,
                ApiModuleName = "Archive wiki document",
                LinkUrl = "/api/v1/Wiki/Archive/\\d+",
                ControllerName = "Wiki",
                ActionName = "Archive",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50158L,
                ApiModuleName = "Delete wiki document",
                LinkUrl = "/api/v1/Wiki/Delete/\\d+",
                ControllerName = "Wiki",
                ActionName = "Delete",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50159L,
                ApiModuleName = "Restore wiki document",
                LinkUrl = "/api/v1/Wiki/Restore/\\d+",
                ControllerName = "Wiki",
                ActionName = "Restore",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50160L,
                ApiModuleName = "Update wiki document access policy",
                LinkUrl = "/api/v1/Wiki/UpdateAccessPolicy/\\d+",
                ControllerName = "Wiki",
                ActionName = "UpdateAccessPolicy",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50161L,
                ApiModuleName = "Rollback wiki revision",
                LinkUrl = "/api/v1/Wiki/Rollback/\\d+",
                ControllerName = "Wiki",
                ActionName = "Rollback",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50162L,
                ApiModuleName = "Import wiki markdown",
                LinkUrl = "/api/v1/Wiki/ImportMarkdown",
                ControllerName = "Wiki",
                ActionName = "ImportMarkdown",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50163L,
                ApiModuleName = "Export wiki markdown",
                LinkUrl = "/api/v1/Wiki/ExportMarkdown/\\d+",
                ControllerName = "Wiki",
                ActionName = "ExportMarkdown",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50164L,
                ApiModuleName = "Admin get wiki review queue",
                LinkUrl = "/api/v1/Wiki/AdminGetReviewQueue",
                ControllerName = "Wiki",
                ActionName = "AdminGetReviewQueue",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50165L,
                ApiModuleName = "Admin get wiki draft evidence",
                LinkUrl = "/api/v1/Wiki/AdminGetDraftById/\\d+",
                ControllerName = "Wiki",
                ActionName = "AdminGetDraftById",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50166L,
                ApiModuleName = "Admin review wiki draft",
                LinkUrl = "/api/v1/Wiki/AdminReviewDraft/\\d+",
                ControllerName = "Wiki",
                ActionName = "AdminReviewDraft",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50174L,
                ApiModuleName = "Get channel discoverability page",
                LinkUrl = "/api/v1/ChannelDiscoverability/GetPage",
                ControllerName = "ChannelDiscoverability",
                ActionName = "GetPage",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50175L,
                ApiModuleName = "Get channel discoverability history",
                LinkUrl = "/api/v1/ChannelDiscoverability/GetHistory",
                ControllerName = "ChannelDiscoverability",
                ActionName = "GetHistory",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50177L,
                ApiModuleName = "Get channel discoverability target",
                LinkUrl = "/api/v1/ChannelDiscoverability/GetById",
                ControllerName = "ChannelDiscoverability",
                ActionName = "GetById",
                Roles = new[] { 10000L, 10001L }
            },
            new
            {
                ApiModuleId = 50176L,
                ApiModuleName = "Update channel discoverability",
                LinkUrl = "/api/v1/ChannelDiscoverability/UpdateVisibility/.+",
                ControllerName = "ChannelDiscoverability",
                ActionName = "UpdateVisibility",
                Roles = new[] { 10000L, 10001L }
            }
        };

        foreach (var item in apiModules)
        {
            var apiExists = await db.Queryable<ApiModule>().AnyAsync(m => m.Id == item.ApiModuleId);
            if (!apiExists)
            {
                var options = new ApiModuleInitializationOptions(item.ApiModuleName, item.LinkUrl)
                {
                    ControllerName = item.ControllerName,
                    ActionName = item.ActionName,
                    IsEnabled = true,
                    IsDeleted = false,
                    IsMenu = false,
                    OrderSort = 0,
                };

                var module = new ApiModule(options)
                {
                    Id = item.ApiModuleId,
                };

                await db.Insertable(module).ExecuteCommandAsync();
            }

            foreach (var roleId in item.Roles)
            {
                await EnsureRoleApiPermissionAsync(db, roleId, item.ApiModuleId, roleId switch
                {
                    10000L => "System",
                    10001L => "Admin",
                    10002L => "Test",
                    _ => roleId.ToString()
                });
            }
        }

        const long systemRoleId = 10000;
        const long adminRoleId = 10001;
        const long testRoleId = 10002;

        await EnsureRoleApiPermissionAsync(db, systemRoleId, 50000, "System");
        await EnsureRoleApiPermissionAsync(db, adminRoleId, 50000, "Admin");
        await EnsureRoleApiPermissionAsync(db, testRoleId, 50000, "Test");
        await RestrictTestRoleApiPermissionsAsync(db);
    }
}
