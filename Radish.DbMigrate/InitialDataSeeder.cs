using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>
/// 初始化基础数据（角色/用户/租户/部门）的 Seed 入口。
/// 后续若需要扩展更多种子数据，可在此类中继续拆分方法。
/// </summary>
internal static partial class InitialDataSeeder
{
    private static bool IsUniqueConstraintViolation(Exception ex, string? token = null)
    {
        var current = ex;
        while (current != null)
        {
            if (!string.IsNullOrWhiteSpace(current.Message) &&
                current.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(token) || current.Message.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            current = current.InnerException!;
        }

        return false;
    }

    private static async Task RunSeedStepAsync(string name, Func<Task> action, ICollection<string> completedSteps)
    {
        var stopwatch = Stopwatch.StartNew();
        Console.WriteLine($"[Radish.DbMigrate] [Seed] 开始：{name}");

        try
        {
            await action();
            stopwatch.Stop();
            completedSteps.Add(name);
            Console.WriteLine($"[Radish.DbMigrate] [Seed] 完成：{name} ({stopwatch.ElapsedMilliseconds} ms)");
        }
        catch
        {
            stopwatch.Stop();
            Console.WriteLine($"[Radish.DbMigrate] [Seed] 失败：{name} ({stopwatch.ElapsedMilliseconds} ms)");
            throw;
        }
    }

    private static Task SeedWikiDocumentsAsync(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<WikiDocument>();
        db.CodeFirst.InitTables<WikiDocumentRevision>();
        Console.WriteLine("[Radish.DbMigrate] 已同步 WikiDocument / WikiDocumentRevision 表结构（自动补齐缺失表/列）。");
        Console.WriteLine("[Radish.DbMigrate] 固定文档改为由 API 启动时自动同步，Seed 阶段跳过。\n");
        return Task.CompletedTask;
    }

    private static Task SeedStickerDefaultsAsync()
    {
        Console.WriteLine("[Radish.DbMigrate] 当前未预置默认表情包/分组种子，跳过。\n");
        return Task.CompletedTask;
    }

    public static async Task SeedAsync(ISqlSugarClient db, IServiceProvider services)
    {
        var completedSteps = new List<string>();
        var seedSteps = new (string Name, Func<Task> Action)[]
        {
            ("角色", () => SeedRolesAsync(db)),
            ("租户", () => SeedTenantsAsync(db)),
            ("部门", () => SeedDepartmentsAsync(db)),
            ("用户", () => SeedUsersAsync(db)),
            ("用户时区偏好", () => SeedUserTimePreferencesAsync(db)),
            ("用户角色", () => SeedUserRolesAsync(db)),
            ("角色 API 权限", () => SeedPermissionsAsync(db)),
            ("Console 授权资源", () => SeedConsoleAuthorizationAsync(db)),
            ("论坛分类", () => SeedForumCategoriesAsync(db)),
            ("论坛标签", () => SeedForumTagsAsync(db)),
            ("Wiki 文档", () => SeedWikiDocumentsAsync(db)),
            ("聊天室默认频道", () => SeedChatChannelsAsync(db)),
            ("等级配置", () => SeedLevelConfigsAsync(db, services)),
            ("商城分类", () => SeedShopCategoriesAsync(db)),
            ("商城商品", () => SeedShopProductsAsync(db)),
            ("表情包默认数据", SeedStickerDefaultsAsync)
        };

        foreach (var step in seedSteps)
        {
            await RunSeedStepAsync(step.Name, step.Action, completedSteps);
        }

        Console.WriteLine("[Radish.DbMigrate] ✓ Seed 完成，共执行以下步骤：");
        foreach (var step in completedSteps)
        {
            Console.WriteLine($"  - {step}");
        }
    }
}
