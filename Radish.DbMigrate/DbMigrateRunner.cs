using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Radish.Common;
using Radish.Common.DbTool;
using Radish.Model;
using Radish.Model.Models;
using Radish.Shared.CustomEnum;
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
        EnsureUserPublicIdentitySchema(mainDb);
        EnsureUserEmailIndex(mainDb);
        EnsureForumIndexes(mainDb);
        EnsureLikeRelationIndexes(mainDb);
        EnsureInventoryBenefitReliabilitySchema(mainDb);
        EnsureRewardBusinessKeySchema(mainDb);
        EnsureContentSubmissionSchema(mainDb);
    }

    private static void EnsureUserPublicIdentitySchema(ISqlSugarClient db)
    {
        const string publicIdIndexName = "idx_user_public_id";
        const string publicIndexIndexName = "idx_user_public_index";

        var entityInfo = db.EntityMaintenance.GetEntityInfo<User>();
        var tableName = entityInfo.DbTableName;
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return;
        }

        var publicIdColumnName = GetColumnName(entityInfo, nameof(User.PublicId));
        if (string.IsNullOrWhiteSpace(publicIdColumnName))
        {
            return;
        }

        var publicIndexColumnName = GetColumnName(entityInfo, nameof(User.PublicIndex));
        if (string.IsNullOrWhiteSpace(publicIndexColumnName))
        {
            return;
        }

        if (!db.DbMaintenance.IsAnyColumn(tableName, publicIdColumnName, false) ||
            !db.DbMaintenance.IsAnyColumn(tableName, publicIndexColumnName, false))
        {
            db.CodeFirst.InitTables<User>();
            Console.WriteLine("[Radish.DbMigrate] 已同步 User 公共身份字段。");
        }

        if (!db.DbMaintenance.IsAnyColumn(tableName, publicIdColumnName, false))
        {
            Console.WriteLine("[Radish.DbMigrate] User.PublicId 字段仍未补齐，已跳过旧用户 PublicId 回填。");
        }
        else
        {
            BackfillMissingUserPublicIds(db);
        }

        if (!db.DbMaintenance.IsAnyColumn(tableName, publicIndexColumnName, false))
        {
            Console.WriteLine("[Radish.DbMigrate] User.PublicIndex 字段仍未补齐，已跳过旧用户 PublicIndex 回填。");
        }
        else
        {
            NormalizeReservedUserPublicIndexes(db);
            BackfillMissingUserPublicIndexes(db);
        }

        if (!db.DbMaintenance.IsAnyIndex(publicIdIndexName))
        {
            var created = db.DbMaintenance.CreateIndex(tableName, [nameof(User.PublicId)], publicIdIndexName, true);
            Console.WriteLine(created
                ? $"[Radish.DbMigrate] 已补齐唯一索引 {publicIdIndexName}。"
                : $"[Radish.DbMigrate] 唯一索引 {publicIdIndexName} 创建未生效，请检查数据库状态。");
        }

        if (!db.DbMaintenance.IsAnyIndex(publicIndexIndexName))
        {
            var created = db.DbMaintenance.CreateIndex(tableName, [nameof(User.PublicIndex)], publicIndexIndexName, true);
            Console.WriteLine(created
                ? $"[Radish.DbMigrate] 已补齐唯一索引 {publicIndexIndexName}。"
                : $"[Radish.DbMigrate] 唯一索引 {publicIndexIndexName} 创建未生效，请检查数据库状态。");
        }
    }

    private static void BackfillMissingUserPublicIds(ISqlSugarClient db)
    {
        var userIds = db.Queryable<User>()
            .Where(user => user.PublicId == null || user.PublicId == string.Empty)
            .Select(user => user.Id)
            .ToList();

        if (userIds.Count == 0)
        {
            return;
        }

        var updatedCount = 0;
        foreach (var userId in userIds)
        {
            var publicId = User.GeneratePublicId();
            var affectedRows = db.Updateable<User>()
                .SetColumns(user => new User
                {
                    PublicId = publicId,
                    UpdateTime = DateTime.Now
                })
                .Where(user => user.Id == userId &&
                               (user.PublicId == null || user.PublicId == string.Empty))
                .ExecuteCommand();

            updatedCount += affectedRows > 0 ? 1 : 0;
        }

        Console.WriteLine($"[Radish.DbMigrate] 已为 {updatedCount} 个旧用户补齐 PublicId。");
    }

    private static void BackfillMissingUserPublicIndexes(ISqlSugarClient db)
    {
        var users = db.Queryable<User>()
            .Where(user => user.PublicIndex == null || user.PublicIndex <= 0)
            .OrderBy(user => user.Id)
            .ToList();

        if (users.Count == 0)
        {
            return;
        }

        var nextPublicIndex = ResolveNextNormalUserPublicIndex(db);
        var updatedCount = 0;

        foreach (var user in users)
        {
            var publicIndex = ResolveReservedPublicIndex(user) ?? nextPublicIndex++;
            if (db.Queryable<User>().Any(item => item.Id != user.Id && item.PublicIndex == publicIndex))
            {
                publicIndex = nextPublicIndex++;
            }

            var affectedRows = db.Updateable<User>()
                .SetColumns(item => new User
                {
                    PublicIndex = publicIndex,
                    UpdateTime = DateTime.Now
                })
                .Where(item => item.Id == user.Id &&
                               (item.PublicIndex == null || item.PublicIndex <= 0))
                .ExecuteCommand();

            updatedCount += affectedRows > 0 ? 1 : 0;
        }

        Console.WriteLine($"[Radish.DbMigrate] 已为 {updatedCount} 个旧用户补齐 PublicIndex。");
    }

    private static void NormalizeReservedUserPublicIndexes(ISqlSugarClient db)
    {
        const long systemUserId = 20000;
        const long adminUserId = 20001;
        const long testUserId = 20002;

        var clearedCount = db.Updateable<User>()
            .SetColumns(user => new User
            {
                PublicIndex = null,
                UpdateTime = DateTime.Now
            })
            .Where(user => user.Id != systemUserId &&
                           user.Id != adminUserId &&
                           user.Id != testUserId &&
                           (user.PublicIndex == 1 ||
                            user.PublicIndex == 2 ||
                            user.PublicIndex == 3))
            .ExecuteCommand();

        if (clearedCount > 0)
        {
            Console.WriteLine($"[Radish.DbMigrate] 已释放 {clearedCount} 个非种子用户占用的保留 PublicIndex。");
        }

        CorrectSeedUserPublicIndex(db, systemUserId, 1);
        CorrectSeedUserPublicIndex(db, adminUserId, 2);
        CorrectSeedUserPublicIndex(db, testUserId, 3);
    }

    private static void CorrectSeedUserPublicIndex(ISqlSugarClient db, long userId, long publicIndex)
    {
        var updatedCount = db.Updateable<User>()
            .SetColumns(user => new User
            {
                PublicIndex = publicIndex,
                UpdateTime = DateTime.Now
            })
            .Where(user => user.Id == userId &&
                           (user.PublicIndex == null || user.PublicIndex != publicIndex))
            .ExecuteCommand();

        if (updatedCount > 0)
        {
            Console.WriteLine($"[Radish.DbMigrate] 已纠正种子用户 Id={userId} 的 PublicIndex={publicIndex}。");
        }
    }

    private static long ResolveNextNormalUserPublicIndex(ISqlSugarClient db)
    {
        var maxPublicIndex = db.Queryable<User>()
            .Where(user => user.PublicIndex >= User.PublicIndexStart)
            .Max(user => user.PublicIndex);

        return maxPublicIndex.GetValueOrDefault(User.PublicIndexStart - 1) + 1;
    }

    private static long? ResolveReservedPublicIndex(User user)
    {
        return user.Id switch
        {
            20000 => 1,
            20001 => 2,
            20002 => 3,
            _ => null
        };
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

    private static void EnsureUserEmailIndex(ISqlSugarClient db)
    {
        const string indexName = "idx_user_email_active";

        var entityInfo = db.EntityMaintenance.GetEntityInfo<User>();
        var tableName = entityInfo.DbTableName;
        if (!db.DbMaintenance.IsAnyTable(tableName, false) || db.DbMaintenance.IsAnyIndex(indexName))
        {
            return;
        }

        var created = db.DbMaintenance.CreateIndex(
            tableName,
            [nameof(User.TenantId), nameof(User.UserEmail), nameof(User.IsDeleted), nameof(User.IsEnable)],
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

    private static void EnsureLikeRelationIndexes(ISqlSugarClient db)
    {
        NormalizePostLikeRelations(db);
        NormalizeCommentLikeRelations(db);

        EnsureIndex(
            db,
            db.EntityMaintenance.GetEntityInfo<UserPostLike>().DbTableName,
            "idx_userpostlike_tenant_user_post",
            [nameof(UserPostLike.TenantId), nameof(UserPostLike.UserId), nameof(UserPostLike.PostId)],
            unique: true);

        EnsureIndex(
            db,
            db.EntityMaintenance.GetEntityInfo<UserPostLike>().DbTableName,
            "idx_userpostlike_post_active",
            [nameof(UserPostLike.TenantId), nameof(UserPostLike.PostId), nameof(UserPostLike.IsDeleted)]);

        EnsureIndex(
            db,
            db.EntityMaintenance.GetEntityInfo<UserCommentLike>().DbTableName,
            "idx_usercommentlike_tenant_user_comment",
            [nameof(UserCommentLike.TenantId), nameof(UserCommentLike.UserId), nameof(UserCommentLike.CommentId)],
            unique: true);

        EnsureIndex(
            db,
            db.EntityMaintenance.GetEntityInfo<UserCommentLike>().DbTableName,
            "idx_usercommentlike_comment_active",
            [nameof(UserCommentLike.TenantId), nameof(UserCommentLike.CommentId), nameof(UserCommentLike.IsDeleted)]);
    }

    private static void EnsureInventoryBenefitReliabilitySchema(ISqlSugarClient db)
    {
        var grantRecordTableName = db.EntityMaintenance.GetEntityInfo<UserInventoryGrantRecord>().DbTableName;
        if (!db.DbMaintenance.IsAnyTable(grantRecordTableName, false))
        {
            db.CodeFirst.InitTables<UserInventoryGrantRecord>();
            Console.WriteLine("[Radish.DbMigrate] 已补齐用户背包发放流水表。");
        }

        NormalizeUserBenefitPurchaseSources(db);
        NormalizeUserInventoryItems(db);
        BackfillInventoryGrantRecords(db);

        EnsureIndex(
            db,
            db.EntityMaintenance.GetEntityInfo<UserBenefit>().DbTableName,
            "idx_benefit_tenant_source_order",
            [nameof(UserBenefit.TenantId), nameof(UserBenefit.SourceOrderId)],
            unique: true);

        EnsureIndex(
            db,
            db.EntityMaintenance.GetEntityInfo<UserInventory>().DbTableName,
            "idx_inventory_tenant_user_type_value",
            [nameof(UserInventory.TenantId), nameof(UserInventory.UserId), nameof(UserInventory.ConsumableType), nameof(UserInventory.ItemValue)],
            unique: true);

        EnsureIndex(
            db,
            grantRecordTableName,
            "idx_inventory_grant_tenant_source_order",
            [nameof(UserInventoryGrantRecord.TenantId), nameof(UserInventoryGrantRecord.SourceOrderId)],
            unique: true);

        EnsureIndex(
            db,
            grantRecordTableName,
            "idx_inventory_grant_inventory",
            [nameof(UserInventoryGrantRecord.TenantId), nameof(UserInventoryGrantRecord.InventoryId)]);
    }

    private static void EnsureRewardBusinessKeySchema(ISqlSugarClient db)
    {
        BackfillCoinRewardBusinessKeys(db);
        BackfillExperienceRewardBusinessKeys(db);

        EnsureIndex(
            db,
            db.EntityMaintenance.GetEntityInfo<CoinTransaction>().DbTableName,
            "idx_coin_reward_business_key",
            [nameof(CoinTransaction.TenantId), nameof(CoinTransaction.RewardBusinessKey)],
            unique: true);

        EnsureIndex(
            db,
            db.EntityMaintenance.GetEntityInfo<ExpTransaction>().DbTableName,
            "idx_exp_reward_business_key",
            [nameof(ExpTransaction.TenantId), nameof(ExpTransaction.RewardBusinessKey)],
            unique: true);
    }

    private static void EnsureContentSubmissionSchema(ISqlSugarClient db)
    {
        var tableName = db.EntityMaintenance.GetEntityInfo<ContentSubmissionRecord>().DbTableName;
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            db.CodeFirst.InitTables<ContentSubmissionRecord>();
            Console.WriteLine("[Radish.DbMigrate] 已补齐论坛内容提交意图记录表。");
        }

        EnsureIndex(
            db,
            tableName,
            "idx_content_submission_client",
            [nameof(ContentSubmissionRecord.TenantId), nameof(ContentSubmissionRecord.UserId), nameof(ContentSubmissionRecord.OperationType), nameof(ContentSubmissionRecord.ClientSubmissionId)],
            unique: true);

        EnsureIndex(
            db,
            tableName,
            "idx_content_submission_fingerprint",
            [nameof(ContentSubmissionRecord.TenantId), nameof(ContentSubmissionRecord.UserId), nameof(ContentSubmissionRecord.OperationType), nameof(ContentSubmissionRecord.ContentFingerprint), nameof(ContentSubmissionRecord.CreateTime)]);

        EnsureIndex(
            db,
            tableName,
            "idx_content_submission_expires",
            [nameof(ContentSubmissionRecord.ExpiresAt)]);

        EnsureIndex(
            db,
            tableName,
            "idx_content_submission_result",
            [nameof(ContentSubmissionRecord.ResultType), nameof(ContentSubmissionRecord.ResultId)]);
    }

    private static void BackfillCoinRewardBusinessKeys(ISqlSugarClient db)
    {
        var tableName = db.EntityMaintenance.GetEntityInfo<CoinTransaction>().DbTableName;
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return;
        }

        var transactions = db.Queryable<CoinTransaction>()
            .Where(transaction => transaction.Status == "SUCCESS")
            .ToList()
            .Select(transaction => new
            {
                Transaction = transaction,
                RewardBusinessKey = BuildCoinRewardBusinessKey(transaction)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.RewardBusinessKey))
            .ToList();

        var updatedCount = 0;
        foreach (var group in transactions.GroupBy(item => new { item.Transaction.TenantId, item.RewardBusinessKey }))
        {
            if (group.Any(item => item.Transaction.RewardBusinessKey == item.RewardBusinessKey))
            {
                continue;
            }

            var keeper = group
                .OrderBy(item => item.Transaction.CreateTime)
                .ThenBy(item => item.Transaction.Id)
                .First()
                .Transaction;

            updatedCount += db.Updateable<CoinTransaction>()
                .SetColumns(transaction => new CoinTransaction
                {
                    RewardBusinessKey = group.Key.RewardBusinessKey,
                    ModifyTime = DateTime.Now
                })
                .Where(transaction => transaction.Id == keeper.Id && transaction.RewardBusinessKey == null)
                .ExecuteCommand();
        }

        if (updatedCount > 0)
        {
            Console.WriteLine($"[Radish.DbMigrate] 已回填 {updatedCount} 条萝卜币奖励业务键。");
        }
    }

    private static void BackfillExperienceRewardBusinessKeys(ISqlSugarClient db)
    {
        var tableName = db.EntityMaintenance.GetEntityInfo<ExpTransaction>().DbTableName;
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return;
        }

        var transactions = db.Queryable<ExpTransaction>()
            .ToList()
            .Select(transaction => new
            {
                Transaction = transaction,
                RewardBusinessKey = BuildExperienceRewardBusinessKey(transaction)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.RewardBusinessKey))
            .ToList();

        var updatedCount = 0;
        foreach (var group in transactions.GroupBy(item => new { item.Transaction.TenantId, item.RewardBusinessKey }))
        {
            if (group.Any(item => item.Transaction.RewardBusinessKey == item.RewardBusinessKey))
            {
                continue;
            }

            var keeper = group
                .OrderBy(item => item.Transaction.CreateTime)
                .ThenBy(item => item.Transaction.Id)
                .First()
                .Transaction;

            updatedCount += db.Updateable<ExpTransaction>()
                .SetColumns(transaction => new ExpTransaction
                {
                    RewardBusinessKey = group.Key.RewardBusinessKey,
                    ModifyTime = DateTime.Now
                })
                .Where(transaction => transaction.Id == keeper.Id && transaction.RewardBusinessKey == null)
                .ExecuteCommand();
        }

        if (updatedCount > 0)
        {
            Console.WriteLine($"[Radish.DbMigrate] 已回填 {updatedCount} 条经验奖励业务键。");
        }
    }

    private static void NormalizeUserBenefitPurchaseSources(ISqlSugarClient db)
    {
        var benefitTableName = db.EntityMaintenance.GetEntityInfo<UserBenefit>().DbTableName;
        if (!db.DbMaintenance.IsAnyTable(benefitTableName, false))
        {
            return;
        }

        var linkedBenefitIds = db.DbMaintenance.IsAnyTable(db.EntityMaintenance.GetEntityInfo<Order>().DbTableName, false)
            ? db.Queryable<Order>()
                .Where(order => order.UserBenefitId.HasValue)
                .Select(order => order.UserBenefitId!.Value)
                .ToList()
                .ToHashSet()
            : [];

        var benefits = db.Queryable<UserBenefit>()
            .Where(benefit => benefit.SourceOrderId.HasValue)
            .ToList();

        var duplicateIds = benefits
            .GroupBy(benefit => new { benefit.TenantId, benefit.SourceOrderId })
            .SelectMany(group => group
                .OrderBy(benefit => linkedBenefitIds.Contains(benefit.Id) ? 0 : 1)
                .ThenBy(benefit => benefit.IsDeleted ? 1 : 0)
                .ThenBy(benefit => benefit.CreateTime)
                .ThenBy(benefit => benefit.Id)
                .Skip(1)
                .Select(benefit => benefit.Id))
            .ToList();

        if (duplicateIds.Count == 0)
        {
            return;
        }

        db.Deleteable<UserBenefit>()
            .Where(benefit => duplicateIds.Contains(benefit.Id))
            .ExecuteCommand();
        Console.WriteLine($"[Radish.DbMigrate] 已清理 {duplicateIds.Count} 条重复订单权益发放记录。");
    }

    private static void NormalizeUserInventoryItems(ISqlSugarClient db)
    {
        var inventoryTableName = db.EntityMaintenance.GetEntityInfo<UserInventory>().DbTableName;
        if (!db.DbMaintenance.IsAnyTable(inventoryTableName, false))
        {
            return;
        }

        var items = db.Queryable<UserInventory>().ToList();
        var duplicateIds = new List<long>();
        var updatedCount = 0;

        foreach (var group in items.GroupBy(item => new
                 {
                     item.TenantId,
                     item.UserId,
                     item.ConsumableType,
                     ItemValue = NormalizeInventoryItemValue(item.ItemValue)
                 }))
        {
            var orderedItems = group
                .OrderBy(item => item.IsDeleted ? 1 : 0)
                .ThenByDescending(item => item.Quantity)
                .ThenByDescending(item => item.ModifyTime ?? item.CreateTime)
                .ThenByDescending(item => item.Id)
                .ToList();
            var keeper = orderedItems[0];
            var totalQuantity = group.Sum(item => Math.Max(item.Quantity, 0));
            var normalizedItemValue = group.Key.ItemValue;

            if (keeper.ItemValue != normalizedItemValue ||
                keeper.Quantity != totalQuantity ||
                keeper.IsDeleted)
            {
                updatedCount += db.Updateable<UserInventory>()
                    .SetColumns(item => new UserInventory
                    {
                        ItemValue = normalizedItemValue,
                        Quantity = totalQuantity,
                        IsDeleted = false,
                        ModifyTime = DateTime.UtcNow
                    })
                    .Where(item => item.Id == keeper.Id)
                    .ExecuteCommand();
            }

            duplicateIds.AddRange(orderedItems.Skip(1).Select(item => item.Id));
        }

        if (duplicateIds.Count > 0)
        {
            db.Deleteable<UserInventory>()
                .Where(item => duplicateIds.Contains(item.Id))
                .ExecuteCommand();
            Console.WriteLine($"[Radish.DbMigrate] 已合并 {duplicateIds.Count} 条重复背包聚合项。");
        }

        if (updatedCount > 0)
        {
            Console.WriteLine($"[Radish.DbMigrate] 已规整 {updatedCount} 条背包聚合项。");
        }
    }

    private static void BackfillInventoryGrantRecords(ISqlSugarClient db)
    {
        var grantRecordTableName = db.EntityMaintenance.GetEntityInfo<UserInventoryGrantRecord>().DbTableName;
        var orderTableName = db.EntityMaintenance.GetEntityInfo<Order>().DbTableName;
        var inventoryTableName = db.EntityMaintenance.GetEntityInfo<UserInventory>().DbTableName;
        if (!db.DbMaintenance.IsAnyTable(grantRecordTableName, false) ||
            !db.DbMaintenance.IsAnyTable(orderTableName, false) ||
            !db.DbMaintenance.IsAnyTable(inventoryTableName, false))
        {
            return;
        }

        var existingSourceOrderIds = db.Queryable<UserInventoryGrantRecord>()
            .Select(record => record.SourceOrderId)
            .ToList()
            .ToHashSet();
        var inventoryIds = db.Queryable<UserInventory>()
            .Select(item => item.Id)
            .ToList()
            .ToHashSet();

        var records = db.Queryable<Order>()
            .Where(order =>
                order.ProductType == ProductType.Consumable &&
                order.Status == OrderStatus.Completed &&
                order.UserBenefitId.HasValue &&
                order.ConsumableType.HasValue)
            .ToList()
            .Where(order => !existingSourceOrderIds.Contains(order.Id) && inventoryIds.Contains(order.UserBenefitId!.Value))
            .Select(order => new UserInventoryGrantRecord
            {
                Id = order.Id,
                TenantId = order.TenantId,
                UserId = order.UserId,
                InventoryId = order.UserBenefitId!.Value,
                SourceOrderId = order.Id,
                SourceProductId = order.ProductId,
                ConsumableType = order.ConsumableType!.Value,
                ItemValue = NormalizeInventoryItemValue(order.BenefitValue),
                Quantity = Math.Max(order.Quantity, 1),
                CreateTime = order.CompletedTime ?? order.CreateTime,
                CreateBy = "System",
                CreateId = order.UserId
            })
            .ToList();

        if (records.Count == 0)
        {
            return;
        }

        db.Insertable(records).ExecuteCommand();
        Console.WriteLine($"[Radish.DbMigrate] 已回填 {records.Count} 条消耗品订单发放流水。");
    }

    private static void NormalizePostLikeRelations(ISqlSugarClient db)
    {
        var likeTableName = db.EntityMaintenance.GetEntityInfo<UserPostLike>().DbTableName;
        var postTableName = db.EntityMaintenance.GetEntityInfo<Post>().DbTableName;
        if (!db.DbMaintenance.IsAnyTable(likeTableName, false) || !db.DbMaintenance.IsAnyTable(postTableName, false))
        {
            return;
        }

        var likes = db.Queryable<UserPostLike>().ToList();
        var duplicateIds = likes
            .GroupBy(like => new { like.TenantId, like.UserId, like.PostId })
            .SelectMany(group => group
                .OrderBy(like => like.IsDeleted ? 1 : 0)
                .ThenByDescending(like => like.LikedAt)
                .ThenByDescending(like => like.Id)
                .Skip(1)
                .Select(like => like.Id))
            .ToList();

        if (duplicateIds.Count > 0)
        {
            db.Deleteable<UserPostLike>()
                .Where(like => duplicateIds.Contains(like.Id))
                .ExecuteCommand();
            Console.WriteLine($"[Radish.DbMigrate] 已清理 {duplicateIds.Count} 条重复帖子点赞关系。");
        }

        var activeLikeCounts = db.Queryable<UserPostLike>()
            .Where(like => !like.IsDeleted)
            .ToList()
            .GroupBy(like => new { like.TenantId, like.PostId })
            .ToDictionary(group => (group.Key.TenantId, group.Key.PostId), group => group.Count());

        var posts = db.Queryable<Post>()
            .Select(post => new Post
            {
                Id = post.Id,
                TenantId = post.TenantId,
                LikeCount = post.LikeCount
            })
            .ToList();

        var updatedCount = 0;
        foreach (var post in posts)
        {
            var expectedCount = activeLikeCounts.GetValueOrDefault((post.TenantId, post.Id), 0);
            if (post.LikeCount == expectedCount)
            {
                continue;
            }

            updatedCount += db.Updateable<Post>()
                .SetColumns(target => new Post { LikeCount = expectedCount })
                .Where(target => target.Id == post.Id)
                .ExecuteCommand();
        }

        if (updatedCount > 0)
        {
            Console.WriteLine($"[Radish.DbMigrate] 已校准 {updatedCount} 条帖子点赞计数。");
        }
    }

    private static void NormalizeCommentLikeRelations(ISqlSugarClient db)
    {
        var likeTableName = db.EntityMaintenance.GetEntityInfo<UserCommentLike>().DbTableName;
        var commentTableName = db.EntityMaintenance.GetEntityInfo<Comment>().DbTableName;
        if (!db.DbMaintenance.IsAnyTable(likeTableName, false) || !db.DbMaintenance.IsAnyTable(commentTableName, false))
        {
            return;
        }

        var likes = db.Queryable<UserCommentLike>().ToList();
        var duplicateIds = likes
            .GroupBy(like => new { like.TenantId, like.UserId, like.CommentId })
            .SelectMany(group => group
                .OrderBy(like => like.IsDeleted ? 1 : 0)
                .ThenByDescending(like => like.LikedAt)
                .ThenByDescending(like => like.Id)
                .Skip(1)
                .Select(like => like.Id))
            .ToList();

        if (duplicateIds.Count > 0)
        {
            db.Deleteable<UserCommentLike>()
                .Where(like => duplicateIds.Contains(like.Id))
                .ExecuteCommand();
            Console.WriteLine($"[Radish.DbMigrate] 已清理 {duplicateIds.Count} 条重复评论点赞关系。");
        }

        var activeLikeCounts = db.Queryable<UserCommentLike>()
            .Where(like => !like.IsDeleted)
            .ToList()
            .GroupBy(like => new { like.TenantId, like.CommentId })
            .ToDictionary(group => (group.Key.TenantId, group.Key.CommentId), group => group.Count());

        var comments = db.Queryable<Comment>()
            .Select(comment => new Comment
            {
                Id = comment.Id,
                TenantId = comment.TenantId,
                LikeCount = comment.LikeCount
            })
            .ToList();

        var updatedCount = 0;
        foreach (var comment in comments)
        {
            var expectedCount = activeLikeCounts.GetValueOrDefault((comment.TenantId, comment.Id), 0);
            if (comment.LikeCount == expectedCount)
            {
                continue;
            }

            updatedCount += db.Updateable<Comment>()
                .SetColumns(target => new Comment { LikeCount = expectedCount })
                .Where(target => target.Id == comment.Id)
                .ExecuteCommand();
        }

        if (updatedCount > 0)
        {
            Console.WriteLine($"[Radish.DbMigrate] 已校准 {updatedCount} 条评论点赞计数。");
        }
    }

    private static void EnsureIndex(
        ISqlSugarClient db,
        string tableName,
        string indexName,
        string[] columns,
        bool unique = false)
    {
        if (!db.DbMaintenance.IsAnyTable(tableName, false) || db.DbMaintenance.IsAnyIndex(indexName))
        {
            return;
        }

        var created = db.DbMaintenance.CreateIndex(tableName, columns, indexName, unique);
        Console.WriteLine(created
            ? $"[Radish.DbMigrate] 已补齐{(unique ? "唯一" : string.Empty)}索引 {indexName}。"
            : $"[Radish.DbMigrate] {(unique ? "唯一" : string.Empty)}索引 {indexName} 创建未生效，请检查数据库状态。");
    }

    private static string NormalizeInventoryItemValue(string? itemValue)
    {
        return itemValue ?? string.Empty;
    }

    private static string? BuildCoinRewardBusinessKey(CoinTransaction transaction)
    {
        if (!transaction.ToUserId.HasValue || !transaction.BusinessId.HasValue)
        {
            return null;
        }

        var userId = transaction.ToUserId.Value;
        var businessId = transaction.BusinessId.Value;
        var dayKey = transaction.CreateTime.ToString("yyyyMMdd");

        return transaction.BusinessType switch
        {
            "POST_LIKE" => $"coin:post-like:author:{userId}:post:{businessId}:day:{dayKey}",
            "POST_LIKE_ACTION" => $"coin:post-like:giver:{userId}:post:{businessId}:day:{dayKey}",
            "COMMENT_LIKE" => $"coin:comment-like:author:{userId}:comment:{businessId}:day:{dayKey}",
            "COMMENT_LIKE_ACTION" => $"coin:comment-like:giver:{userId}:comment:{businessId}:day:{dayKey}",
            "COMMENT_POST" => $"coin:comment-create:author:{userId}:comment:{businessId}",
            "COMMENT_REPLY" => $"coin:comment-reply:author:{userId}:comment:{businessId}:day:{dayKey}",
            "GOD_COMMENT" => $"coin:highlight-base:god-comment:author:{userId}:comment:{businessId}",
            "SOFA" => $"coin:highlight-base:sofa:author:{userId}:comment:{businessId}",
            _ when TryParseRetentionRewardBusinessType(transaction.BusinessType, out var highlightType, out var week) =>
                $"coin:highlight-retention:{highlightType}:highlight:{businessId}:week:{week}:author:{userId}",
            _ => null
        };
    }

    private static string? BuildExperienceRewardBusinessKey(ExpTransaction transaction)
    {
        if (transaction.UserId <= 0)
        {
            return null;
        }

        var userId = transaction.UserId;
        var dayKey = transaction.CreatedDate.ToString("yyyyMMdd");
        var targetType = transaction.BusinessType?.ToLowerInvariant();

        return transaction.ExpType switch
        {
            "POST_CREATE" when transaction.BusinessId.HasValue =>
                $"exp:post-create:author:{userId}:post:{transaction.BusinessId.Value}",
            "FIRST_POST" => $"exp:first-post:user:{userId}",
            "COMMENT_CREATE" when transaction.BusinessId.HasValue =>
                $"exp:comment-create:author:{userId}:comment:{transaction.BusinessId.Value}",
            "FIRST_COMMENT" => $"exp:first-comment:user:{userId}",
            "RECEIVE_LIKE" when transaction.BusinessId.HasValue && (targetType == "post" || targetType == "comment") =>
                $"exp:receive-like:{targetType}:user:{userId}:target:{transaction.BusinessId.Value}:day:{dayKey}",
            "GIVE_LIKE" when transaction.BusinessId.HasValue && (targetType == "post" || targetType == "comment") =>
                $"exp:give-like:{targetType}:user:{userId}:target:{transaction.BusinessId.Value}:day:{dayKey}",
            "GOD_COMMENT" when transaction.BusinessId.HasValue =>
                $"exp:highlight-base:god-comment:author:{userId}:comment:{transaction.BusinessId.Value}",
            "SOFA_COMMENT" when transaction.BusinessId.HasValue =>
                $"exp:highlight-base:sofa:author:{userId}:comment:{transaction.BusinessId.Value}",
            _ => null
        };
    }

    private static bool TryParseRetentionRewardBusinessType(string? businessType, out string highlightType, out int week)
    {
        highlightType = string.Empty;
        week = 0;

        if (string.IsNullOrWhiteSpace(businessType))
        {
            return false;
        }

        const string suffixPrefix = "_RETENTION_W";
        var suffixIndex = businessType.IndexOf(suffixPrefix, StringComparison.Ordinal);
        if (suffixIndex <= 0)
        {
            return false;
        }

        var rawHighlightType = businessType[..suffixIndex];
        var rawWeek = businessType[(suffixIndex + suffixPrefix.Length)..];
        if (!int.TryParse(rawWeek, out week) || week is < 1 or > 3)
        {
            return false;
        }

        if (string.Equals(rawHighlightType, "GodComment", StringComparison.OrdinalIgnoreCase))
        {
            highlightType = "god-comment";
            return true;
        }

        if (string.Equals(rawHighlightType, "Sofa", StringComparison.OrdinalIgnoreCase))
        {
            highlightType = "sofa";
            return true;
        }

        return false;
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
