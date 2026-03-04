using System;
using System.Threading.Tasks;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>
/// 初始化基础数据（角色/用户/租户/部门）的 Seed 入口。
/// 后续若需要扩展更多种子数据，可在此类中继续拆分方法。
/// </summary>
internal static partial class InitialDataSeeder
{
    public static async Task SeedAsync(ISqlSugarClient db, IServiceProvider services)
    {
        await SeedRolesAsync(db);
        await SeedTenantsAsync(db);
        await SeedDepartmentsAsync(db);
        await SeedUsersAsync(db);
        await SeedUserTimePreferencesAsync(db);
        await SeedUserRolesAsync(db);
        await SeedPermissionsAsync(db);
        await SeedForumCategoriesAsync(db);
        await SeedForumTagsAsync(db);
        await SeedChatChannelsAsync(db);
        await SeedLevelConfigsAsync(db, services);
        await SeedShopCategoriesAsync(db);
        await SeedShopProductsAsync(db);

        Console.WriteLine("[Radish.DbMigrate] ✓ Seed 完成:");
        Console.WriteLine("  - 角色/租户/部门/用户/用户角色");
        Console.WriteLine("  - 用户时区偏好");
        Console.WriteLine("  - 角色-API 权限/论坛分类/标签");
        Console.WriteLine("  - 聊天室默认频道");
        Console.WriteLine("  - 等级配置/商城分类/商品");
        Console.WriteLine("  - 表情包：不预置默认分组/表情（仅由 init 保证表结构）");
    }
}
