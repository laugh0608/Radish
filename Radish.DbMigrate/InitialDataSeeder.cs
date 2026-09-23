using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    internal static async Task RunSeedStepAsync(SeedStep step, Func<Task> action)
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        using var scope = Serilog.Context.LogContext.PushProperty("seedStep", step.ToString());
        try
        {
            await action();
            succeeded = true;
        }
        finally
        {
            // 只记录阶段结果；异常原样交给 RuntimeProcess，不回放载荷或重复记录 Error。
            Serilog.Log.ForContext("EventCode", "dbmigrate.seed.step_finished")
                .ForContext("SourceCategory", "database")
                .ForContext("outcome", succeeded ? "succeeded" : "failed")
                .ForContext("durationMs", Stopwatch.GetElapsedTime(started).TotalMilliseconds)
                .Information("Seed stage finished");
        }
    }

    private static void WriteSeedEvent(string code, int? count = null, bool warning = false)
    {
        var logger = Serilog.Log.ForContext("EventCode", code).ForContext("SourceCategory", "database");
        if (count.HasValue) logger = logger.ForContext("count", count.Value);
        if (warning) logger.Warning("Seed event {EventCode}", code);
        else logger.Information("Seed event {EventCode}", code);
    }

    private static Task SeedWikiDocumentsAsync(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<WikiDocument>();
        db.CodeFirst.InitTables<WikiDocumentRevision>();

        return Task.CompletedTask;
    }

    private static Task SeedStickerDefaultsAsync()
    {
        // 表情包由管理端维护；保留阶段边界，不注入默认数据。
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
        var developerDefaultsSeed = EvaluateDeveloperDefaultsSeed();
        var seedSteps = new List<(SeedStep Step, Func<Task> Action)>
        {
            (SeedStep.Roles, () => SeedRolesAsync(db)),
            (SeedStep.Tenants, () => SeedTenantsAsync(db)),
            (SeedStep.Departments, () => SeedDepartmentsAsync(db)),
            (SeedStep.ApiPermissions, () => SeedPermissionsAsync(db)),
            (SeedStep.ConsoleAuthorization, () => SeedConsoleAuthorizationAsync(db)),
            (SeedStep.ForumCategories, () => SeedForumCategoriesAsync(db)),
            (SeedStep.ForumTags, () => SeedForumTagsAsync(db)),
            (SeedStep.WikiDocuments, () => SeedWikiDocumentsAsync(db)),
            (SeedStep.ChatChannels, () => SeedChatChannelsAsync(db)),
            (SeedStep.Levels, () => SeedLevelConfigsAsync(db, services)),
            (SeedStep.ShopCategories, () => SeedShopCategoriesAsync(db)),
            (SeedStep.ShopProducts, () => SeedShopProductsAsync(db)),
            (SeedStep.ShopOrderSnapshots, () => BackfillShopOrderStockTypesAsync(db)),
            (SeedStep.ShopImages, () => SeedShopDefaultImagesAsync(db)),
            (SeedStep.Stickers, SeedStickerDefaultsAsync)
        };

        WriteSeedEvent(developerDefaultsSeed.ShouldSeed
            ? "dbmigrate.seed.developer_defaults_enabled"
            : "dbmigrate.seed.developer_defaults_skipped");
        if (developerDefaultsSeed.ShouldSeed)
        {
            seedSteps.InsertRange(3,
            [
                (SeedStep.DeveloperUsers, () => SeedUsersAsync(db)),
                (SeedStep.DeveloperTimePreferences, () => SeedUserTimePreferencesAsync(db)),
                (SeedStep.DeveloperUserRoles, () => SeedUserRolesAsync(db))
            ]);
        }

        foreach (var step in seedSteps)
        {
            await RunSeedStepAsync(step.Step, step.Action);
            completedStepCount++;
        }

        WriteSeedEvent("dbmigrate.seed.completed", completedStepCount);
    }
}

internal enum SeedStep
{
    Roles,
    Tenants,
    Departments,
    ApiPermissions,
    ConsoleAuthorization,
    ForumCategories,
    ForumTags,
    WikiDocuments,
    ChatChannels,
    Levels,
    ShopCategories,
    ShopProducts,
    ShopOrderSnapshots,
    ShopImages,
    Stickers,
    DeveloperUsers,
    DeveloperTimePreferences,
    DeveloperUserRoles
}
