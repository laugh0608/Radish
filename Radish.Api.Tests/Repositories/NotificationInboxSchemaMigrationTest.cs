using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.DbMigrate;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class NotificationInboxSchemaMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";
    private static readonly DateTime NowUtc = new(2026, 7, 18, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Apply_ShouldBackfillHistoricalInboxAndRemainRepeatable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-notification-inbox-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);
        using var services = CreateServices();
        try
        {
            var notificationTable = CreateLegacySchema(db);
            InsertLegacyNotification(db, notificationTable);
            db.Ado.ExecuteCommand(
                "INSERT INTO \"UserNotification\" " +
                "(\"Id\", \"UserId\", \"NotificationId\", \"IsRead\", \"ReadAt\", " +
                "\"DeliveryStatus\", \"DeliveredAt\", \"RetryCount\", \"LastRetryAt\", " +
                "\"IsDeleted\", \"DeletedAt\", \"TenantId\", \"CreateTime\", \"CreateBy\", \"CreateId\") " +
                "VALUES (2001, 3001, 1001, 0, NULL, 'Created', NULL, 0, NULL, 0, NULL, 9, " +
                "'2026-07-17 08:00:00', 'System', 0)");
            db.Ado.ExecuteCommand(
                "INSERT INTO \"NotificationSetting\" " +
                "(\"Id\", \"UserId\", \"NotificationType\", \"IsEnabled\", \"EnableInApp\", " +
                "\"EnableEmail\", \"EnableSound\", \"TenantId\", \"CreateTime\", \"CreateBy\", \"CreateId\") " +
                "VALUES (4001, 3001, 'PostLiked', 1, 0, 0, 1, 9, '2026-07-17 08:00:00', 'User', 3001)");

            var migration = NotificationInboxSchemaMigration.Instance;
            Assert.Empty(migration.Diagnose(db, services));
            migration.Apply(db, services);
            var currentNotification = new Notification(new NotificationInitializationOptions(
                NotificationType.PostLiked,
                "新通知")
            {
                Category = NotificationCategory.Reaction,
                TemplateKey = "notification.PostLiked",
                TemplateArgumentsJson = "{\"targetTitle\":\"新帖子\"}",
                TargetKind = NotificationTargetKind.ForumPost,
                TargetDataJson = new NotificationTargetData { PostId = 7101 }.ToJson(),
                OccurredAtUtc = NowUtc,
                TenantId = 9
            })
            {
                Id = 1002,
                BusinessKey = "notification:new:1002",
                CreateTime = NowUtc
            };
            db.Insertable(currentNotification).SplitTable().ExecuteCommand();
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            var relation = Assert.Single(db.Queryable<UserNotification>().ToList());
            var group = Assert.Single(db.Queryable<NotificationInboxGroup>().ToList());
            var state = Assert.Single(db.Queryable<NotificationInboxState>().ToList());
            var preference = Assert.Single(db.Queryable<NotificationSetting>().ToList());
            var notifications = db.Queryable<Notification>().SplitTable().ToList();
            var notification = Assert.Single(notifications, item => item.Id == 1001);
            var preservedCurrent = Assert.Single(notifications, item => item.Id == 1002);

            Assert.Equal(group.Id, relation.InboxGroupId);
            Assert.Equal(1, group.OccurrenceCount);
            Assert.Equal(1, group.UnreadOccurrenceCount);
            Assert.Equal(1, group.DistinctTriggerCount);
            Assert.Equal(1, state.UnreadGroupCount);
            Assert.Equal(1, state.UnreadOccurrenceCount);
            Assert.Equal(NotificationCategory.Reaction, preference.Category);
            Assert.False(preference.InAppEnabled);
            Assert.Equal(NotificationCategory.Discussion, notification.Category);
            Assert.Equal("notification.legacy", notification.TemplateKey);
            Assert.Equal(NotificationTargetKind.ForumPost, notification.TargetKind);
            Assert.Equal("notification.PostLiked", preservedCurrent.TemplateKey);
            Assert.Equal("{\"targetTitle\":\"新帖子\"}", preservedCurrent.TemplateArgumentsJson);
            Assert.Equal(NotificationTargetKind.ForumPost, preservedCurrent.TargetKind);
            Assert.Equal(NowUtc, preservedCurrent.OccurredAtUtc);
            Assert.Equal(
                new DateTime(2026, 7, 17, 8, 0, 0, DateTimeKind.Utc),
                DateTime.SpecifyKind(notification.OccurredAtUtc, DateTimeKind.Utc));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Apply_ShouldRejectNonDefaultLegacyDeliveryState()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-notification-delivery-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);
        using var services = CreateServices();
        try
        {
            var notificationTable = CreateLegacySchema(db);
            InsertLegacyNotification(db, notificationTable);
            db.Ado.ExecuteCommand(
                "INSERT INTO \"UserNotification\" " +
                "(\"Id\", \"UserId\", \"NotificationId\", \"IsRead\", \"ReadAt\", " +
                "\"DeliveryStatus\", \"DeliveredAt\", \"RetryCount\", \"LastRetryAt\", " +
                "\"IsDeleted\", \"DeletedAt\", \"TenantId\", \"CreateTime\", \"CreateBy\", \"CreateId\") " +
                "VALUES (2001, 3001, 1001, 0, NULL, 'Delivered', '2026-07-17 08:01:00', 0, NULL, " +
                "0, NULL, 9, '2026-07-17 08:00:00', 'System', 0)");

            var migration = NotificationInboxSchemaMigration.Instance;
            Assert.Contains(
                migration.Diagnose(db, services),
                issue => issue.Contains("非默认 delivery", StringComparison.Ordinal));
            Assert.Throws<InvalidOperationException>(() => migration.Apply(db, services));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task Apply_ShouldUpgradeLegacyPostgreSqlSchema()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过通知 PostgreSQL 迁移测试");

        var schema = $"notification_inbox_{Guid.NewGuid():N}";
        using var adminDb = PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "admin",
            ConnectionString = adminConnectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA \"{schema}\"");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
            {
                ConfigId = "Message",
                ConnectionString = connectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
            using var services = CreateServices();
            db.CodeFirst.InitTables<Notification>();
            var notificationTable = db.DbMaintenance.GetTableInfoList(false)
                .Select(table => table.Name)
                .Single(name => name.StartsWith("notification_", StringComparison.OrdinalIgnoreCase));
            foreach (var columnName in new[]
                     {
                         "Category", "TemplateKey", "TemplateArgumentsJson", "TargetKind", "TargetDataJson",
                         "TargetSchemaVersion", "OccurredAtUtc"
                     })
            {
                DropColumn(db, notificationTable, columnName);
            }

            db.CodeFirst.InitTables<UserNotification>();
            db.Ado.ExecuteCommand("DROP INDEX IF EXISTS idx_user_notification_group_unread");
            DropColumn(db, "UserNotification", "InboxGroupId");
            DropColumn(db, "UserNotification", "OccurredAtUtc");
            var relationId = DatabaseIdentifierResolver.ResolveColumn(db, "UserNotification", "Id")
                             ?? throw new InvalidOperationException("UserNotification.Id 不存在");
            var lowercase = relationId.ColumnName == relationId.ColumnName.ToLowerInvariant();
            string Physical(string name) => lowercase ? name.ToLowerInvariant() : name;
            db.Ado.ExecuteCommand(
                $"ALTER TABLE \"{relationId.TableName}\" " +
                $"ADD COLUMN \"{Physical("DeliveryStatus")}\" text NOT NULL DEFAULT 'Created', " +
                $"ADD COLUMN \"{Physical("DeliveredAt")}\" timestamp with time zone NULL, " +
                $"ADD COLUMN \"{Physical("RetryCount")}\" integer NOT NULL DEFAULT 0, " +
                $"ADD COLUMN \"{Physical("LastRetryAt")}\" timestamp with time zone NULL");
            InsertLegacyNotificationPortable(db, notificationTable);
            InsertLegacyRelationPortable(db);

            var migration = NotificationInboxSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.Single(db.Queryable<NotificationInboxGroup>().ToList());
            Assert.Single(db.Queryable<NotificationInboxState>().ToList());

            using var repositoryScope = PostgreSqlIntegrationSqlSugarFactory.CreateScope(new ConnectionConfig
            {
                ConfigId = "message",
                ConnectionString = connectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
            var repository = new NotificationRepository(new UnitOfWorkManage(
                repositoryScope,
                NullLogger<UnitOfWorkManage>.Instance));
            var group = repositoryScope.GetConnectionScope("message")
                .Queryable<NotificationInboxGroup>()
                .Single();
            await repository.DeleteGroupAsync(9, 3001, group.Id, NowUtc);
            var cleanup = await repository.CleanupAsync(NowUtc.AddDays(31), 10, 5000);

            Assert.Equal(1, cleanup.DeletedRelationCount);
            Assert.Equal(1, cleanup.DeletedGroupCount);
            Assert.Equal(1, cleanup.DeletedNotificationCount);
            Assert.Empty(cleanup.CapacityWarnings);
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    private static string CreateLegacySchema(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<Notification>();
        var notificationTable = db.DbMaintenance.GetTableInfoList(false)
            .Select(table => table.Name)
            .Single(name => name.StartsWith("Notification_", StringComparison.Ordinal));
        foreach (var columnName in new[]
                 {
                     "Category", "TemplateKey", "TemplateArgumentsJson", "TargetKind", "TargetDataJson",
                     "TargetSchemaVersion", "OccurredAtUtc"
                 })
        {
            db.Ado.ExecuteCommand($"ALTER TABLE \"{notificationTable}\" DROP COLUMN \"{columnName}\"");
        }

        db.CodeFirst.InitTables<UserNotification>();
        db.Ado.ExecuteCommand("DROP INDEX IF EXISTS \"idx_user_notification_group_unread\"");
        db.Ado.ExecuteCommand("ALTER TABLE \"UserNotification\" DROP COLUMN \"InboxGroupId\"");
        db.Ado.ExecuteCommand("ALTER TABLE \"UserNotification\" DROP COLUMN \"OccurredAtUtc\"");
        foreach (var command in new[]
                 {
                     "ALTER TABLE \"UserNotification\" ADD COLUMN \"DeliveryStatus\" TEXT NOT NULL DEFAULT 'Created'",
                     "ALTER TABLE \"UserNotification\" ADD COLUMN \"DeliveredAt\" DATETIME NULL",
                     "ALTER TABLE \"UserNotification\" ADD COLUMN \"RetryCount\" INTEGER NOT NULL DEFAULT 0",
                     "ALTER TABLE \"UserNotification\" ADD COLUMN \"LastRetryAt\" DATETIME NULL"
                 })
        {
            db.Ado.ExecuteCommand(command);
        }

        db.Ado.ExecuteCommand("DROP TABLE IF EXISTS \"NotificationSetting\"");
        db.Ado.ExecuteCommand(
            "CREATE TABLE \"NotificationSetting\" (" +
            "\"Id\" INTEGER NOT NULL PRIMARY KEY, \"UserId\" INTEGER NOT NULL, " +
            "\"NotificationType\" TEXT NOT NULL, \"IsEnabled\" INTEGER NOT NULL, " +
            "\"EnableInApp\" INTEGER NOT NULL, \"EnableEmail\" INTEGER NOT NULL, " +
            "\"EnableSound\" INTEGER NOT NULL, \"TenantId\" INTEGER NOT NULL, " +
            "\"CreateTime\" DATETIME NOT NULL, \"CreateBy\" TEXT NOT NULL, \"CreateId\" INTEGER NOT NULL, " +
            "\"ModifyTime\" DATETIME NULL, \"ModifyBy\" TEXT NULL, \"ModifyId\" INTEGER NULL)");
        return notificationTable;
    }

    private static void InsertLegacyNotification(ISqlSugarClient db, string tableName)
    {
        db.Ado.ExecuteCommand(
            $"INSERT INTO \"{tableName}\" " +
            "(\"Id\", \"Type\", \"Priority\", \"Title\", \"Content\", \"BusinessKey\", " +
            "\"BusinessType\", \"BusinessId\", \"TriggerId\", \"TriggerName\", \"TriggerAvatar\", " +
            "\"ExtData\", \"TenantId\", \"CreateTime\", \"CreateBy\", \"CreateId\") VALUES " +
            "(1001, 'CommentReplied', 2, '评论回复', '内容', 'notification:legacy:1001', " +
            "'Comment', 5001, 6001, 'Tester', NULL, " +
            "'{\"app\":\"forum\",\"postId\":\"7001\",\"commentId\":\"5001\"}', " +
            "9, '2026-07-17 08:00:00', 'System', 0)");
    }

    private static void InsertLegacyNotificationPortable(ISqlSugarClient db, string tableName)
    {
        var names = new[]
        {
            "Id", "Type", "Priority", "Title", "Content", "BusinessKey", "BusinessType", "BusinessId",
            "TriggerId", "TriggerName", "TriggerAvatar", "ExtData", "TenantId", "CreateTime", "CreateBy", "CreateId"
        };
        var columns = names.ToDictionary(
            name => name,
            name => DatabaseIdentifierResolver.ResolveColumn(db, tableName, name)
                    ?? throw new InvalidOperationException($"{tableName}.{name} 不存在"));
        db.Ado.ExecuteCommand(
            $"INSERT INTO \"{tableName}\" ({string.Join(", ", names.Select(name => $"\"{columns[name].ColumnName}\""))}) " +
            "VALUES (@Id, @Type, @Priority, @Title, @Content, @BusinessKey, @BusinessType, @BusinessId, " +
            "@TriggerId, @TriggerName, NULL, @ExtData, @TenantId, @CreateTime, @CreateBy, @CreateId)",
            new SugarParameter("@Id", 1001L),
            new SugarParameter("@Type", "CommentReplied"),
            new SugarParameter("@Priority", 2),
            new SugarParameter("@Title", "评论回复"),
            new SugarParameter("@Content", "内容"),
            new SugarParameter("@BusinessKey", "notification:legacy:1001"),
            new SugarParameter("@BusinessType", "Comment"),
            new SugarParameter("@BusinessId", 5001L),
            new SugarParameter("@TriggerId", 6001L),
            new SugarParameter("@TriggerName", "Tester"),
            new SugarParameter("@ExtData", "{\"app\":\"forum\",\"postId\":\"7001\",\"commentId\":\"5001\"}"),
            new SugarParameter("@TenantId", 9L),
            new SugarParameter("@CreateTime", NowUtc.AddDays(-1)),
            new SugarParameter("@CreateBy", "System"),
            new SugarParameter("@CreateId", 0L));
    }

    private static void InsertLegacyRelationPortable(ISqlSugarClient db)
    {
        var names = new[]
        {
            "Id", "UserId", "NotificationId", "IsRead", "ReadAt", "IsDeleted", "DeletedAt", "TenantId",
            "CreateTime", "CreateBy", "CreateId"
        };
        var columns = names.ToDictionary(
            name => name,
            name => DatabaseIdentifierResolver.ResolveColumn(db, "UserNotification", name)
                    ?? throw new InvalidOperationException($"UserNotification.{name} 不存在"));
        var table = columns["Id"].TableName;
        db.Ado.ExecuteCommand(
            $"INSERT INTO \"{table}\" ({string.Join(", ", names.Select(name => $"\"{columns[name].ColumnName}\""))}) " +
            "VALUES (@Id, @UserId, @NotificationId, @IsRead, NULL, @IsDeleted, NULL, @TenantId, " +
            "@CreateTime, @CreateBy, @CreateId)",
            new SugarParameter("@Id", 2001L),
            new SugarParameter("@UserId", 3001L),
            new SugarParameter("@NotificationId", 1001L),
            new SugarParameter("@IsRead", false),
            new SugarParameter("@IsDeleted", false),
            new SugarParameter("@TenantId", 9L),
            new SugarParameter("@CreateTime", NowUtc.AddDays(-1)),
            new SugarParameter("@CreateBy", "System"),
            new SugarParameter("@CreateId", 0L));
    }

    private static void DropColumn(ISqlSugarClient db, string tableName, string columnName)
    {
        var column = DatabaseIdentifierResolver.ResolveColumn(db, tableName, columnName)
                     ?? throw new InvalidOperationException($"{tableName}.{columnName} 不存在");
        db.Ado.ExecuteCommand(
            $"ALTER TABLE \"{column.TableName}\" DROP COLUMN \"{column.ColumnName}\"");
    }

    private static SqlSugarScope CreateClient(string path)
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "Message",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    private static ServiceProvider CreateServices()
    {
        return new ServiceCollection()
            .AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(NowUtc)))
            .BuildServiceProvider();
    }

    private sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }
}
