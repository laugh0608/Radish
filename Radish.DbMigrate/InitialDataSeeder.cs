using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Radish.Common;
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

    private static async Task<int> RunSeedStepAsync(string name, Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        var originalOut = Console.Out;
        using var capturedOutput = new StringWriter(CultureInfo.InvariantCulture);

        Console.SetOut(capturedOutput);

        try
        {
            await action();
            stopwatch.Stop();
            Console.SetOut(originalOut);

            var detailLines = GetCapturedSeedDetailLines(capturedOutput.ToString());
            Console.WriteLine($"[Radish.DbMigrate] [Seed] 完成：{name}，明细 {detailLines.Count} 条 ({stopwatch.ElapsedMilliseconds} ms)");
            return detailLines.Count;
        }
        catch
        {
            stopwatch.Stop();
            Console.SetOut(originalOut);

            var detailLines = GetCapturedSeedDetailLines(capturedOutput.ToString());
            Console.WriteLine($"[Radish.DbMigrate] [Seed] 失败：{name}，失败前明细 {detailLines.Count} 条 ({stopwatch.ElapsedMilliseconds} ms)");
            WriteCapturedSeedDetails(name, detailLines);
            throw;
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static IReadOnlyList<string> GetCapturedSeedDetailLines(string output)
    {
        var detailLines = new List<string>();
        using var reader = new StringReader(output);

        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                detailLines.Add(line);
            }
        }

        return detailLines;
    }

    private static void WriteCapturedSeedDetails(string name, IReadOnlyCollection<string> detailLines)
    {
        if (detailLines.Count == 0)
        {
            return;
        }

        Console.WriteLine($"[Radish.DbMigrate] [Seed] {name} 失败前明细：");
        foreach (var line in detailLines)
        {
            Console.WriteLine(line);
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

    private static DeveloperDefaultsSeedDecision EvaluateDeveloperDefaultsSeed()
    {
        var enabledValue = AppSettingsTool.RadishApp("Seed", "DeveloperDefaultsEnabled");
        var stageValue = AppSettingsTool.RadishApp("RadishDeployment", "Stage");
        return DeveloperDefaultsSeedPolicy.Evaluate(enabledValue, stageValue);
    }

    public static async Task SeedAsync(ISqlSugarClient db, IServiceProvider services)
    {
        var completedStepCount = 0;
        var totalDetailLineCount = 0;
        var developerDefaultsSeed = EvaluateDeveloperDefaultsSeed();
        var seedSteps = new List<(string Name, Func<Task> Action)>
        {
            ("角色", () => SeedRolesAsync(db)),
            ("租户", () => SeedTenantsAsync(db)),
            ("部门", () => SeedDepartmentsAsync(db)),
            ("角色 API 权限", () => SeedPermissionsAsync(db)),
            ("Console 授权资源", () => SeedConsoleAuthorizationAsync(db)),
            ("论坛分类", () => SeedForumCategoriesAsync(db)),
            ("论坛标签", () => SeedForumTagsAsync(db)),
            ("Wiki 文档", () => SeedWikiDocumentsAsync(db)),
            ("聊天室默认频道", () => SeedChatChannelsAsync(db)),
            ("等级配置", () => SeedLevelConfigsAsync(db, services)),
            ("商城分类", () => SeedShopCategoriesAsync(db)),
            ("商城商品", () => SeedShopProductsAsync(db)),
            ("商城订单快照", () => BackfillShopOrderStockTypesAsync(db)),
            ("商城默认图片", () => SeedShopDefaultImagesAsync(db)),
            ("表情包默认数据", SeedStickerDefaultsAsync)
        };

        if (developerDefaultsSeed.ShouldSeed)
        {
            Console.WriteLine($"[Radish.DbMigrate] {developerDefaultsSeed.Message}");
            seedSteps.InsertRange(3,
            [
                ("开发默认用户", () => SeedUsersAsync(db)),
                ("开发默认用户时区偏好", () => SeedUserTimePreferencesAsync(db)),
                ("开发默认用户角色", () => SeedUserRolesAsync(db))
            ]);
        }
        else
        {
            Console.WriteLine($"[Radish.DbMigrate] {developerDefaultsSeed.Message}");
        }

        foreach (var step in seedSteps)
        {
            totalDetailLineCount += await RunSeedStepAsync(step.Name, step.Action);
            completedStepCount++;
        }

        Console.WriteLine($"[Radish.DbMigrate] ✓ Seed 完成，共执行 {completedStepCount} 个分类，明细合计 {totalDetailLineCount} 条。");
    }
}
