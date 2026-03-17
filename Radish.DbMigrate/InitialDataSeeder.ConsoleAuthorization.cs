using System.Data;
using Radish.Common.PermissionTool;
using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

internal static partial class InitialDataSeeder
{
    private sealed record ConsoleResourceSeed(
        long Id,
        long ParentId,
        string ResourceKey,
        string ResourceName,
        string ResourceType,
        string ModuleKey,
        string? RoutePath,
        int OrderSort,
        bool ShowInSidebar,
        bool ShowInSearch,
        string? Description = null,
        string? Icon = null);

    private sealed record ConsoleResourceApiSeed(long ResourceId, string LinkUrl, string RelationType);

    private static readonly ConsoleResourceSeed[] ConsoleResourceSeeds =
    [
        new(60990, 0, ConsolePermissions.Access, "Console 访问", "Entry", "console-access", null, 1, false, false, "控制 WebOS Console 入口与 Console SPA 访问"),
        new(61000, 0, ConsolePermissions.DashboardView, "仪表盘", "Page", "dashboard", "/", 10, true, true, "Dashboard 页面"),
        new(61010, 0, ConsolePermissions.ApplicationsView, "应用管理", "Page", "applications", "/applications", 20, true, true, "应用管理页面"),
        new(61011, 61010, ConsolePermissions.ApplicationsCreate, "新增应用", "Button", "applications", null, 21, false, false),
        new(61012, 61010, ConsolePermissions.ApplicationsEdit, "编辑应用", "Button", "applications", null, 22, false, false),
        new(61013, 61010, ConsolePermissions.ApplicationsDelete, "删除应用", "Button", "applications", null, 23, false, false),
        new(61014, 61010, ConsolePermissions.ApplicationsResetSecret, "重置密钥", "Button", "applications", null, 24, false, false),
        new(61020, 0, ConsolePermissions.ProductsView, "商品管理", "Page", "products", "/products", 30, true, true, "商品管理页面"),
        new(61021, 61020, ConsolePermissions.ProductsCreate, "新增商品", "Button", "products", null, 31, false, false),
        new(61022, 61020, ConsolePermissions.ProductsEdit, "编辑商品", "Button", "products", null, 32, false, false),
        new(61023, 61020, ConsolePermissions.ProductsDelete, "删除商品", "Button", "products", null, 33, false, false),
        new(61024, 61020, ConsolePermissions.ProductsToggleSale, "上下架商品", "Button", "products", null, 34, false, false),
        new(61030, 0, ConsolePermissions.OrdersView, "订单管理", "Page", "orders", "/orders", 40, true, true, "订单管理页面"),
        new(61031, 61030, ConsolePermissions.OrdersRetry, "重试发放", "Button", "orders", null, 41, false, false),
        new(61040, 0, ConsolePermissions.UsersView, "用户管理", "Page", "users", "/users", 50, true, true, "用户管理页面"),
        new(61050, 0, ConsolePermissions.RolesView, "角色管理", "Page", "roles", "/roles", 60, true, true, "角色管理页面"),
        new(61051, 61050, ConsolePermissions.RolesCreate, "新增角色", "Button", "roles", null, 61, false, false),
        new(61052, 61050, ConsolePermissions.RolesEdit, "编辑角色", "Button", "roles", null, 62, false, false),
        new(61053, 61050, ConsolePermissions.RolesToggle, "启停角色", "Button", "roles", null, 63, false, false),
        new(61054, 61050, ConsolePermissions.RolesDelete, "删除角色", "Button", "roles", null, 64, false, false),
        new(61060, 0, ConsolePermissions.TagsView, "标签管理", "Page", "tags", "/tags", 70, true, true, "标签管理页面"),
        new(61061, 61060, ConsolePermissions.TagsCreate, "新增标签", "Button", "tags", null, 71, false, false),
        new(61062, 61060, ConsolePermissions.TagsEdit, "编辑标签", "Button", "tags", null, 72, false, false),
        new(61063, 61060, ConsolePermissions.TagsDelete, "删除标签", "Button", "tags", null, 73, false, false),
        new(61064, 61060, ConsolePermissions.TagsRestore, "恢复标签", "Button", "tags", null, 74, false, false),
        new(61065, 61060, ConsolePermissions.TagsToggle, "启停标签", "Button", "tags", null, 75, false, false),
        new(61066, 61060, ConsolePermissions.TagsSort, "标签排序", "Button", "tags", null, 76, false, false),
        new(61070, 0, ConsolePermissions.StickersView, "表情包管理", "Page", "stickers", "/stickers", 80, true, true, "表情包管理页面"),
        new(61071, 61070, ConsolePermissions.StickersCreate, "新增表情包", "Button", "stickers", null, 81, false, false),
        new(61072, 61070, ConsolePermissions.StickersEdit, "编辑表情包", "Button", "stickers", null, 82, false, false),
        new(61073, 61070, ConsolePermissions.StickersDelete, "删除表情包", "Button", "stickers", null, 83, false, false),
        new(61074, 61070, ConsolePermissions.StickersToggle, "启停表情包", "Button", "stickers", null, 84, false, false),
        new(61075, 61070, ConsolePermissions.StickersSort, "表情包排序", "Button", "stickers", null, 85, false, false),
        new(61076, 61070, ConsolePermissions.StickersBatchUpload, "批量上传表情包", "Button", "stickers", null, 86, false, false),
        new(61080, 0, ConsolePermissions.SystemConfigView, "系统配置", "Page", "system-config", "/system-config", 90, true, true, "系统配置页面"),
        new(61081, 61080, ConsolePermissions.SystemConfigCreate, "新增配置", "Button", "system-config", null, 91, false, false),
        new(61082, 61080, ConsolePermissions.SystemConfigEdit, "编辑配置", "Button", "system-config", null, 92, false, false),
        new(61083, 61080, ConsolePermissions.SystemConfigDelete, "删除配置", "Button", "system-config", null, 93, false, false),
        new(61090, 0, ConsolePermissions.HangfireView, "定时任务", "Entry", "hangfire", "/hangfire", 100, true, true, "Hangfire Dashboard")
    ];

    private static readonly ConsoleResourceApiSeed[] ConsoleResourceApiSeeds =
    [
        new(61000, "/api/v1/Statistics/GetDashboardStats", "View"),
        new(61010, "/api/v1/Client/GetClients", "View"),
        new(61010, "/api/v1/Client/GetClient/.+", "View"),
        new(61011, "/api/v1/Client/CreateClient", "Action"),
        new(61012, "/api/v1/Client/GetClient/.+", "View"),
        new(61012, "/api/v1/Client/UpdateClient/.+", "Action"),
        new(61013, "/api/v1/Client/DeleteClient/.+", "Action"),
        new(61014, "/api/v1/Client/ResetClientSecret/.+", "Action"),
        new(61020, "/api/v1/Shop/GetCategories", "View"),
        new(61020, "/api/v1/Shop/AdminGetProducts", "View"),
        new(61021, "/api/v1/Shop/CreateProduct", "Action"),
        new(61022, "/api/v1/Shop/UpdateProduct", "Action"),
        new(61023, "/api/v1/Shop/DeleteProduct/.+", "Action"),
        new(61024, "/api/v1/Shop/PutOnSale/.+", "Action"),
        new(61024, "/api/v1/Shop/TakeOffSale/.+", "Action"),
        new(61030, "/api/v1/Shop/AdminGetOrders", "View"),
        new(61031, "/api/v1/Shop/RetryGrantBenefit/.+", "Action"),
        new(61040, "/api/v1/User/GetUserList", "View"),
        new(61040, "/api/v1/User/GetUserById/\\d+", "View"),
        new(61050, "/api/v1/Role/GetRoleList", "View"),
        new(61050, "/api/v1/Role/GetRoleById", "View"),
        new(61051, "/api/v1/Role/CreateRole", "Action"),
        new(61052, "/api/v1/Role/GetRoleById", "View"),
        new(61052, "/api/v1/Role/UpdateRole", "Action"),
        new(61053, "/api/v1/Role/ToggleRoleStatus", "Action"),
        new(61054, "/api/v1/Role/DeleteRole", "Action"),
        new(61060, "/api/v1/Tag/GetPage", "View"),
        new(61061, "/api/v1/Tag/Create", "Action"),
        new(61062, "/api/v1/Tag/Update/.+", "Action"),
        new(61063, "/api/v1/Tag/Delete/.+", "Action"),
        new(61064, "/api/v1/Tag/Restore/.+", "Action"),
        new(61065, "/api/v1/Tag/ToggleStatus/.+", "Action"),
        new(61066, "/api/v1/Tag/UpdateSort/.+", "Action"),
        new(61070, "/api/v1/Sticker/GetAdminGroups", "View"),
        new(61070, "/api/v1/Sticker/GetGroupStickers/.+", "View"),
        new(61071, "/api/v1/Sticker/CreateGroup", "Action"),
        new(61071, "/api/v1/Sticker/AddSticker", "Action"),
        new(61072, "/api/v1/Sticker/UpdateGroup/.+", "Action"),
        new(61072, "/api/v1/Sticker/UpdateSticker/.+", "Action"),
        new(61073, "/api/v1/Sticker/DeleteGroup/.+", "Action"),
        new(61073, "/api/v1/Sticker/DeleteSticker/.+", "Action"),
        new(61074, "/api/v1/Sticker/UpdateGroup/.+", "Action"),
        new(61075, "/api/v1/Sticker/BatchUpdateSort", "Action"),
        new(61076, "/api/v1/Sticker/BatchAddStickers", "Action"),
        new(61076, "/api/v1/Sticker/NormalizeCode", "Action"),
        new(61071, "/api/v1/Sticker/CheckGroupCode", "Action"),
        new(61072, "/api/v1/Sticker/CheckGroupCode", "Action"),
        new(61071, "/api/v1/Sticker/CheckStickerCode", "Action"),
        new(61072, "/api/v1/Sticker/CheckStickerCode", "Action"),
        new(61080, "/api/v1/SystemConfig/GetSystemConfigs", "View"),
        new(61080, "/api/v1/SystemConfig/GetConfigCategories", "View"),
        new(61080, "/api/v1/SystemConfig/GetConfigById", "View"),
        new(61081, "/api/v1/SystemConfig/CreateConfig", "Action"),
        new(61082, "/api/v1/SystemConfig/GetConfigById", "View"),
        new(61082, "/api/v1/SystemConfig/UpdateConfig", "Action"),
        new(61083, "/api/v1/SystemConfig/DeleteConfig", "Action"),
        new(61090, "/hangfire(/.*)?", "View")
    ];

    private static async Task SeedConsoleAuthorizationAsync(ISqlSugarClient db)
    {
        await EnsureConsoleAuthorizationTablesAsync(db);
        db.CodeFirst.InitTables<ConsoleResource, RoleConsoleResource, ConsoleResourceApiModule>();
        Console.WriteLine("[Radish.DbMigrate] 已同步 Console 授权相关表结构。");

        foreach (var spec in ConsoleResourceSeeds)
        {
            var existing = await db.Queryable<ConsoleResource>()
                .FirstAsync(item => item.ResourceKey == spec.ResourceKey);

            if (existing == null)
            {
                await db.Insertable(new ConsoleResource
                {
                    Id = spec.Id,
                    ParentId = spec.ParentId,
                    ResourceKey = spec.ResourceKey,
                    ResourceName = spec.ResourceName,
                    ResourceType = spec.ResourceType,
                    ModuleKey = spec.ModuleKey,
                    RoutePath = spec.RoutePath,
                    Icon = spec.Icon,
                    OrderSort = spec.OrderSort,
                    ShowInSidebar = spec.ShowInSidebar,
                    ShowInSearch = spec.ShowInSearch,
                    Description = spec.Description,
                    IsEnabled = true,
                    IsDeleted = false,
                    DeletedAt = null,
                    DeletedBy = null,
                    CreateBy = "System",
                    CreateId = 0,
                    ModifyBy = "System",
                    ModifyId = 0,
                    ModifyTime = DateTime.UtcNow
                }).ExecuteCommandAsync();
                continue;
            }

            await db.Updateable<ConsoleResource>()
                .SetColumns(item => new ConsoleResource
                {
                    ParentId = spec.ParentId,
                    ResourceName = spec.ResourceName,
                    ResourceType = spec.ResourceType,
                    ModuleKey = spec.ModuleKey,
                    RoutePath = spec.RoutePath,
                    Icon = spec.Icon,
                    OrderSort = spec.OrderSort,
                    ShowInSidebar = spec.ShowInSidebar,
                    ShowInSearch = spec.ShowInSearch,
                    Description = spec.Description,
                    IsEnabled = true,
                    IsDeleted = false,
                    DeletedAt = null,
                    DeletedBy = null,
                    ModifyBy = "System",
                    ModifyId = 0,
                    ModifyTime = DateTime.UtcNow
                })
                .Where(item => item.Id == existing.Id)
                .ExecuteCommandAsync();
        }

        var resourceMap = (await db.Queryable<ConsoleResource>()
                .Where(item => !item.IsDeleted)
                .ToListAsync())
            .ToDictionary(item => item.ResourceKey, item => item);

        var apiModuleMap = (await db.Queryable<ApiModule>()
                .Where(item => !item.IsDeleted && item.IsEnabled)
                .ToListAsync())
            .ToDictionary(item => item.LinkUrl, item => item, StringComparer.OrdinalIgnoreCase);

        foreach (var spec in ConsoleResourceApiSeeds)
        {
            var resource = ConsoleResourceSeeds.FirstOrDefault(item => item.Id == spec.ResourceId);
            if (resource == null || !resourceMap.TryGetValue(resource.ResourceKey, out var persistedResource))
            {
                continue;
            }

            if (!apiModuleMap.TryGetValue(spec.LinkUrl, out var apiModule))
            {
                Console.WriteLine($"[Radish.DbMigrate] Console 资源映射缺少 ApiModule：{spec.LinkUrl}");
                continue;
            }

            var existing = await db.Queryable<ConsoleResourceApiModule>()
                .FirstAsync(item => item.ConsoleResourceId == persistedResource.Id && item.ApiModuleId == apiModule.Id);

            if (existing == null)
            {
                await db.Insertable(new ConsoleResourceApiModule
                {
                    Id = BuildConsoleResourceApiModuleSeedId(spec.ResourceId, apiModule.Id),
                    ConsoleResourceId = persistedResource.Id,
                    ApiModuleId = apiModule.Id,
                    RelationType = spec.RelationType,
                    IsDeleted = false,
                    DeletedAt = null,
                    DeletedBy = null,
                    CreateBy = "System",
                    CreateId = 0,
                    ModifyBy = "System",
                    ModifyId = 0,
                    ModifyTime = DateTime.UtcNow
                }).ExecuteCommandAsync();
                continue;
            }

            await db.Updateable<ConsoleResourceApiModule>()
                .SetColumns(item => new ConsoleResourceApiModule
                {
                    RelationType = spec.RelationType,
                    IsDeleted = false,
                    DeletedAt = null,
                    DeletedBy = null,
                    ModifyBy = "System",
                    ModifyId = 0,
                    ModifyTime = DateTime.UtcNow
                })
                .Where(item => item.Id == existing.Id)
                .ExecuteCommandAsync();
        }

        var defaultRoleIds = new[] { 10000L, 10001L };
        var allResources = resourceMap.Values.ToList();

        foreach (var roleId in defaultRoleIds)
        {
            foreach (var resource in allResources)
            {
                var existing = await db.Queryable<RoleConsoleResource>()
                    .FirstAsync(item => item.RoleId == roleId && item.ConsoleResourceId == resource.Id);

                if (existing == null)
                {
                    await db.Insertable(new RoleConsoleResource
                    {
                        Id = 630000 + (roleId * 1000) + resource.Id,
                        RoleId = roleId,
                        ConsoleResourceId = resource.Id,
                        IsDeleted = false,
                        DeletedAt = null,
                        DeletedBy = null,
                        CreateBy = "System",
                        CreateId = 0,
                        ModifyBy = "System",
                        ModifyId = 0,
                        ModifyTime = DateTime.UtcNow
                    }).ExecuteCommandAsync();
                    continue;
                }

                await db.Updateable<RoleConsoleResource>()
                    .SetColumns(item => new RoleConsoleResource
                    {
                        IsDeleted = false,
                        DeletedAt = null,
                        DeletedBy = null,
                        ModifyBy = "System",
                        ModifyId = 0,
                        ModifyTime = DateTime.UtcNow
                    })
                    .Where(item => item.Id == existing.Id)
                    .ExecuteCommandAsync();
            }
        }
    }

    private static async Task EnsureConsoleAuthorizationTablesAsync(ISqlSugarClient db)
    {
        if (db.CurrentConnectionConfig.DbType != SqlSugar.DbType.Sqlite)
        {
            return;
        }

        await EnsureNullableSoftDeleteColumnsAsync<ConsoleResource>(db);
        await EnsureNullableSoftDeleteColumnsAsync<RoleConsoleResource>(db);
        await EnsureNullableSoftDeleteColumnsAsync<ConsoleResourceApiModule>(db);
    }

    private static async Task EnsureNullableSoftDeleteColumnsAsync<TEntity>(ISqlSugarClient db)
        where TEntity : class, new()
    {
        var entityInfo = db.EntityMaintenance.GetEntityInfo<TEntity>();
        var tableName = entityInfo.DbTableName;
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return;
        }

        var columnInfo = GetSqliteTableColumns(db, tableName);
        if (columnInfo.Count == 0)
        {
            return;
        }

        var deletedAtNeedsFix = columnInfo.TryGetValue("DeletedAt", out var deletedAt) && deletedAt.IsNotNull;
        var deletedByNeedsFix = columnInfo.TryGetValue("DeletedBy", out var deletedBy) && deletedBy.IsNotNull;

        if (!deletedAtNeedsFix && !deletedByNeedsFix)
        {
            return;
        }

        var backupTableName = $"{tableName}__legacy_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        Console.WriteLine($"[Radish.DbMigrate] 检测到 SQLite 旧表结构：{tableName} 的软删除列仍为 NOT NULL，开始自动重建。");

        await db.Ado.ExecuteCommandAsync($"ALTER TABLE {QuoteIdentifier(tableName)} RENAME TO {QuoteIdentifier(backupTableName)}");
        db.CodeFirst.InitTables<TEntity>();

        var backupColumns = GetSqliteTableColumns(db, backupTableName);
        var newColumns = GetSqliteTableColumns(db, tableName);
        var commonColumns = newColumns.Keys
            .Where(column => backupColumns.ContainsKey(column))
            .ToList();

        if (commonColumns.Count > 0)
        {
            var columnList = string.Join(", ", commonColumns.Select(QuoteIdentifier));
            await db.Ado.ExecuteCommandAsync(
                $"INSERT INTO {QuoteIdentifier(tableName)} ({columnList}) SELECT {columnList} FROM {QuoteIdentifier(backupTableName)}");
        }

        await db.Ado.ExecuteCommandAsync($"DROP TABLE {QuoteIdentifier(backupTableName)}");
        Console.WriteLine($"[Radish.DbMigrate] 已完成 {tableName} 表结构修复。");
    }

    private static Dictionary<string, SqliteColumnInfo> GetSqliteTableColumns(ISqlSugarClient db, string tableName)
    {
        var result = new Dictionary<string, SqliteColumnInfo>(StringComparer.OrdinalIgnoreCase);
        var schemaTable = db.Ado.GetDataTable($"PRAGMA table_info({QuoteIdentifier(tableName)})");

        foreach (DataRow row in schemaTable.Rows)
        {
            var columnName = row["name"]?.ToString();
            if (string.IsNullOrWhiteSpace(columnName))
            {
                continue;
            }

            var notNullValue = row["notnull"]?.ToString();
            result[columnName] = new SqliteColumnInfo(string.Equals(notNullValue, "1", StringComparison.Ordinal));
        }

        return result;
    }

    private static long BuildConsoleResourceApiModuleSeedId(long resourceId, long apiModuleId)
    {
        // 高位保留种子前缀，避免 ResourceId + ApiModuleId 的求和模式发生主键碰撞。
        return 6200000000L + (resourceId * 1000L) + (apiModuleId - 50000L);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private sealed record SqliteColumnInfo(bool IsNotNull);
}
