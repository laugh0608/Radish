using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Radish.Common;
using Radish.Common.DbTool;
using Radish.Model;
using Radish.Model.Models;
using SqlSugar;

namespace Radish.DbMigrate;

internal static class DbMigrateRunner
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration, string environment, string[] args)
    {
        var mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "apply";

        switch (mode)
        {
            case "apply":
                await RunApplyAsync(services, configuration, environment);
                break;

            case "doctor":
            case "verify":
                DbMigrateDoctor.Run(services, configuration, environment);
                break;

            case "init":
                await RunInitAsync(services, configuration, environment);
                break;

            case "seed":
                await RunSeedAsync(services, configuration, environment);
                break;

            default:
                PrintHelp();
                break;
        }
    }

    private static async Task RunApplyAsync(IServiceProvider services, IConfiguration configuration, string environment)
    {
        Console.WriteLine("[Radish.DbMigrate] 默认执行 apply：自动补齐表结构并填充初始数据。");
        await RunSeedAsync(services, configuration, environment);
    }

    private static async Task RunInitAsync(IServiceProvider services, IConfiguration configuration, string environment)
    {
        Console.WriteLine($"[Radish.DbMigrate] Environment: {environment}");

        var db = services.GetRequiredService<ISqlSugarClient>();
        var mainDbConnId = AppSettingsTool.RadishApp("MainDb");

        Console.WriteLine("[Radish.DbMigrate] 创建数据库（如不存在）...");
        foreach (var _ in BaseDbConfig.AllConfigs)
        {
            db.DbMaintenance.CreateDatabase();
        }

        Console.WriteLine("[Radish.DbMigrate] 初始化业务表结构（Code First）...");

        foreach (var config in BaseDbConfig.AllConfigs)
        {
            var dbForConfig = (SqlSugarScope)db;
            var configId = config.ConfigId?.ToString();
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new InvalidOperationException("DbMigrate 遇到缺少 ConfigId 的数据库连接配置，无法继续初始化表结构。");
            }

            var conn = dbForConfig.GetConnectionScope(configId);

            var entityTypesForConfig = DbMigrateEntityRegistry.GetEntityTypesForConfig(configId);

            foreach (var type in entityTypesForConfig)
            {
                Console.WriteLine($"  -> Init table for entity: {type.FullName} (ConnId={config.ConfigId})");
                conn.CodeFirst.InitTables(type);
            }
        }

        EnsureSupplementalIndexes(db, mainDbConnId);

        Console.WriteLine("[Radish.DbMigrate] Init 完成。");
    }

    private static async Task RunSeedAsync(IServiceProvider services, IConfiguration configuration, string environment)
    {
        Console.WriteLine($"[Radish.DbMigrate] Environment: {environment}");

        var db = services.GetRequiredService<ISqlSugarClient>();
        var mainDbConnId = AppSettingsTool.RadishApp("MainDb");

        Console.WriteLine("[Radish.DbMigrate] 检查数据库表结构...");
        var inspectionResult = DbMigrateInspection.InspectSeedReadiness(services, mainDbConnId);

        if (inspectionResult.DatabaseFileMissing || inspectionResult.MissingTables.Count > 0 || inspectionResult.MissingColumns.Count > 0)
        {
            if (inspectionResult.DatabaseFileMissing)
            {
                Console.WriteLine($"[Radish.DbMigrate] ⚠️  检测到主库文件缺失 ({inspectionResult.DatabaseFilePath ?? "<unknown>"})，自动执行 init...");
            }
            else if (inspectionResult.MissingColumns.Count > 0)
            {
                Console.WriteLine($"[Radish.DbMigrate] ⚠️  检测到表结构缺列 ({string.Join(", ", inspectionResult.MissingColumns)})，自动执行 init...");
            }
            else
            {
                Console.WriteLine($"[Radish.DbMigrate] ⚠️  检测到表结构缺失 ({string.Join(", ", inspectionResult.MissingTables)})，自动执行 init...");
            }

            await RunInitAsync(services, configuration, environment);
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("[Radish.DbMigrate] ✓ 数据库表结构已存在");
        }

        EnsureSupplementalIndexes(db, mainDbConnId);

        Console.WriteLine("[Radish.DbMigrate] 开始执行初始数据 Seed...");
        Console.WriteLine("[Radish.DbMigrate] 表情包种子策略：当前不预置默认分组/表情，仅确保表结构可用。");
        await InitialDataSeeder.SeedAsync(db, services);
    }

    private static void EnsureSupplementalIndexes(ISqlSugarClient db, string? mainDbConnId)
    {
        if (db is not SqlSugarScope dbScope)
        {
            return;
        }

        var normalizedMainDbConnId = (string.IsNullOrWhiteSpace(mainDbConnId) ? "Main" : mainDbConnId)
            .ToLowerInvariant();
        var mainDb = dbScope.GetConnectionScope(normalizedMainDbConnId);
        EnsureBootstrapStateSchema(mainDb);
        EnsureUserLoginIndex(mainDb);
        EnsureForumIndexes(mainDb);
    }

    private static void EnsureBootstrapStateSchema(ISqlSugarClient db)
    {
        if (db.CurrentConnectionConfig.DbType != SqlSugar.DbType.PostgreSQL)
        {
            return;
        }

        var entityInfo = db.EntityMaintenance.GetEntityInfo<SystemBootstrapState>();
        var tableName = ResolveExistingTableName(db, entityInfo.DbTableName);
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return;
        }

        DropNotNullIfNeeded(
            db,
            tableName,
            GetColumnName(entityInfo, nameof(SystemBootstrapState.CompletedUserId)));
        DropNotNullIfNeeded(
            db,
            tableName,
            GetColumnName(entityInfo, nameof(SystemBootstrapState.CompletedTime)));
    }

    private static void DropNotNullIfNeeded(ISqlSugarClient db, string tableName, string? expectedColumnName)
    {
        var columnName = ResolveExistingColumnName(db, tableName, expectedColumnName);
        if (string.IsNullOrWhiteSpace(columnName) || !IsPostgreSqlColumnNotNull(db, tableName, columnName))
        {
            return;
        }

        db.Ado.ExecuteCommand($"""
            ALTER TABLE {QuoteIdentifier(tableName)}
            ALTER COLUMN {QuoteIdentifier(columnName)} DROP NOT NULL
            """);
        Console.WriteLine($"[Radish.DbMigrate] 已修复 SystemBootstrapState.{expectedColumnName} 可空约束。");
    }

    private static bool IsPostgreSqlColumnNotNull(ISqlSugarClient db, string tableName, string columnName)
    {
        var isNullable = db.Ado.GetString("""
            SELECT is_nullable
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND lower(table_name) = lower(@tableName)
              AND lower(column_name) = lower(@columnName)
            LIMIT 1
            """,
            new SugarParameter("@tableName", tableName),
            new SugarParameter("@columnName", columnName));

        return string.Equals(isNullable, "NO", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetColumnName(EntityInfo entityInfo, string propertyName)
    {
        return entityInfo.Columns
            .FirstOrDefault(column => string.Equals(column.PropertyName, propertyName, StringComparison.Ordinal))
            ?.DbColumnName;
    }

    private static string? ResolveExistingTableName(ISqlSugarClient db, string? expectedTableName)
    {
        if (string.IsNullOrWhiteSpace(expectedTableName))
        {
            return null;
        }

        return db.Ado.GetString("""
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = current_schema()
              AND lower(table_name) = lower(@tableName)
            LIMIT 1
            """, new SugarParameter("@tableName", expectedTableName));
    }

    private static string? ResolveExistingColumnName(ISqlSugarClient db, string tableName, string? expectedColumnName)
    {
        if (string.IsNullOrWhiteSpace(expectedColumnName))
        {
            return null;
        }

        return db.Ado.GetString("""
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND lower(table_name) = lower(@tableName)
              AND lower(column_name) = lower(@columnName)
            LIMIT 1
            """,
            new SugarParameter("@tableName", tableName),
            new SugarParameter("@columnName", expectedColumnName));
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static void EnsureUserLoginIndex(ISqlSugarClient db)
    {
        const string indexName = "idx_user_login_active";

        var entityInfo = db.EntityMaintenance.GetEntityInfo<User>();
        var tableName = entityInfo.DbTableName;
        if (!db.DbMaintenance.IsAnyTable(tableName, false) || db.DbMaintenance.IsAnyIndex(indexName))
        {
            return;
        }

        var created = db.DbMaintenance.CreateIndex(
            tableName,
            [nameof(User.TenantId), nameof(User.LoginName), nameof(User.IsDeleted), nameof(User.IsEnable)],
            indexName,
            false);

        Console.WriteLine(created
            ? $"[Radish.DbMigrate] 已补齐索引 {indexName}。"
            : $"[Radish.DbMigrate] 索引 {indexName} 创建未生效，请检查数据库状态。");
    }

    private static void EnsureForumIndexes(ISqlSugarClient db)
    {
        EnsureIndex(
            db,
            db.EntityMaintenance.GetEntityInfo<Category>().DbTableName,
            "idx_category_parent_enabled_deleted_sort",
            [nameof(Category.ParentId), nameof(Category.IsEnabled), nameof(Category.IsDeleted), nameof(Category.OrderSort)]);

        EnsureIndex(
            db,
            db.EntityMaintenance.GetEntityInfo<Post>().DbTableName,
            "idx_post_forum_list",
            [nameof(Post.TenantId), nameof(Post.IsDeleted), nameof(Post.IsPublished), nameof(Post.IsTop), nameof(Post.CreateTime)]);

        EnsureIndex(
            db,
            db.EntityMaintenance.GetEntityInfo<Post>().DbTableName,
            "idx_post_forum_category_list",
            [nameof(Post.TenantId), nameof(Post.CategoryId), nameof(Post.IsDeleted), nameof(Post.IsPublished), nameof(Post.CreateTime)]);
    }

    private static void EnsureIndex(ISqlSugarClient db, string tableName, string indexName, string[] columns)
    {
        if (!db.DbMaintenance.IsAnyTable(tableName, false) || db.DbMaintenance.IsAnyIndex(indexName))
        {
            return;
        }

        var created = db.DbMaintenance.CreateIndex(tableName, columns, indexName, false);
        Console.WriteLine(created
            ? $"[Radish.DbMigrate] 已补齐索引 {indexName}。"
            : $"[Radish.DbMigrate] 索引 {indexName} 创建未生效，请检查数据库状态。");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Radish.DbMigrate 用法:");
        Console.WriteLine("  dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj");
        Console.WriteLine("  dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- apply");
        Console.WriteLine("      推荐入口。自动检查数据库、按需 init，并执行 seed。");
        Console.WriteLine("  dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- doctor");
        Console.WriteLine("      只读检查当前配置、连接定义与主库业务表状态。");
        Console.WriteLine("  dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- init");
        Console.WriteLine("      高级命令。仅初始化数据库并基于实体结构创建/更新表结构。");
        Console.WriteLine("  dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- seed");
        Console.WriteLine("      高级命令。执行数据初始化（如缺表会自动先执行 init）。");
    }
}
