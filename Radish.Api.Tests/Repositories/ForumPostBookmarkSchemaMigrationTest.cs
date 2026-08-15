using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Model;
using Radish.Api.Tests.TestCollections;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

[Collection(PostgreSqlIntegrationCollection.CollectionName)]
public sealed class ForumPostBookmarkSchemaMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable =
        "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public void Migration_ShouldNotCreateUnrelatedBaselineTablesForIsolatedLedgerSchema()
    {
        var path = CreatePath("missing-baseline");
        using var db = CreateSqliteClient(path);
        using var services = new ServiceCollection()
            .AddSingleton<ISqlSugarClient>(db)
            .BuildServiceProvider();
        try
        {
            db.CodeFirst.InitTables<Post>();

            ForumPostBookmarkSchemaMigration.Instance.Apply(db, services);

            Assert.True(db.DbMaintenance.IsAnyTable(nameof(UserPostBookmark), false));
            Assert.False(db.DbMaintenance.IsAnyTable(nameof(User), false));
            Assert.True(db.DbMaintenance.IsAnyTable(nameof(Post), false));
            Assert.Empty(ForumPostBookmarkSchemaMigration.Instance.Verify(db, services));
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void Migration_ShouldResetLegacyProjectionAndRemainRepeatableOnSqlite()
    {
        var path = CreatePath("legacy");
        using var db = CreateSqliteClient(path);
        using var services = new ServiceCollection()
            .AddSingleton<ISqlSugarClient>(db)
            .BuildServiceProvider();
        try
        {
            db.CodeFirst.InitTables<User>();
            db.CodeFirst.InitTables<Post>();
            SeedUserAndPost(db, collectCount: 9);

            var migration = ForumPostBookmarkSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.Equal(0, db.Queryable<Post>().InSingle(7001).CollectCount);
            Assert.Empty(db.Queryable<UserPostBookmark>().ToList());
            Assert.Contains(
                SchemaMigrationRegistry.All,
                item => item.MigrationId == "20260729_017_forum_post_bookmark" &&
                        item.Scope == "Main");
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void Migration_ShouldBackfillPublicIdAndRebuildProjectionOnSqlite()
    {
        var path = CreatePath("backfill");
        using var db = CreateSqliteClient(path);
        using var services = new ServiceCollection()
            .AddSingleton<ISqlSugarClient>(db)
            .BuildServiceProvider();
        try
        {
            var migration = ForumPostBookmarkSchemaMigration.Instance;
            db.CodeFirst.InitTables<User>();
            db.CodeFirst.InitTables<Post>();
            SeedUserAndPost(db, collectCount: 0);
            migration.Apply(db, services);
            db.Insertable(new UserPostBookmark
            {
                Id = 8001,
                PublicId = string.Empty,
                TenantId = 9,
                UserId = 1001,
                PostId = 7001,
                BookmarkedAt = DateTime.UtcNow,
                CreateTime = DateTime.UtcNow,
                CreateBy = "seed",
                CreateId = 1001
            }).ExecuteCommand();
            db.Updateable<Post>()
                .SetColumns(item => new Post { CollectCount = 7 })
                .Where(item => item.Id == 7001)
                .ExecuteCommand();

            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.True(UserPostBookmark.HasPublicIdFormat(
                Assert.Single(db.Queryable<UserPostBookmark>().ToList()).PublicId));
            Assert.Equal(1, db.Queryable<Post>().InSingle(7001).CollectCount);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void Verify_ShouldReportCrossTenantOrphanAndProjectionMismatch()
    {
        var path = CreatePath("invalid");
        using var db = CreateSqliteClient(path);
        using var services = new ServiceCollection()
            .AddSingleton<ISqlSugarClient>(db)
            .BuildServiceProvider();
        try
        {
            var migration = ForumPostBookmarkSchemaMigration.Instance;
            db.CodeFirst.InitTables<User>();
            db.CodeFirst.InitTables<Post>();
            SeedUserAndPost(db, collectCount: 0);
            migration.Apply(db, services);
            db.Updateable<Post>()
                .SetColumns(item => new Post { CollectCount = -1 })
                .Where(item => item.Id == 7001)
                .ExecuteCommand();
            db.Insertable(new UserPostBookmark
            {
                Id = 8001,
                PublicId = UserPostBookmark.GeneratePublicId(),
                TenantId = 10,
                UserId = 1001,
                PostId = 7999,
                BookmarkedAt = DateTime.UtcNow,
                CreateTime = DateTime.UtcNow,
                CreateBy = "seed",
                CreateId = 1001
            }).ExecuteCommand();

            var issues = migration.Verify(db, services);

            Assert.Contains(issues, issue => issue.Contains("跨租户的用户", StringComparison.Ordinal));
            Assert.Contains(issues, issue => issue.Contains("不存在或跨租户的帖子", StringComparison.Ordinal));
            Assert.Contains(issues, issue => issue.Contains("CollectCount 小于 0", StringComparison.Ordinal));
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task Migration_ShouldApplyAndVerifyOnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过帖子收藏 PostgreSQL 迁移测试");

        var schema = $"post_bookmark_migration_{Guid.NewGuid():N}";
        using var adminDb = PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "admin",
            ConnectionString = adminConnectionString!,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA \"{schema}\"");
        try
        {
            var connectionString =
                $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = PostgreSqlIntegrationSqlSugarFactory.CreateScope(new ConnectionConfig
            {
                ConfigId = "main",
                ConnectionString = connectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
            using var services = new ServiceCollection()
                .AddSingleton<ISqlSugarClient>(db)
                .BuildServiceProvider();
            db.CodeFirst.InitTables<User>();
            db.CodeFirst.InitTables<Post>();

            ForumPostBookmarkSchemaMigration.Instance.Apply(db, services);
            ForumPostBookmarkSchemaMigration.Instance.Apply(db, services);

            Assert.Empty(ForumPostBookmarkSchemaMigration.Instance.Verify(db, services));
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    private static void SeedUserAndPost(ISqlSugarClient db, int collectCount)
    {
        db.Insertable(new User
        {
            Id = 1001,
            PublicId = "usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f",
            PublicIndex = 1001,
            UserName = "alice",
            UserEmail = "alice@bookmark-migration.test",
            LoginPassword = "hash",
            TenantId = 9,
            IsEnable = true,
            IsDeleted = false
        }).ExecuteCommand();
        db.Insertable(new Post("收藏迁移测试", "用于验证收藏迁移和严格投影校验")
        {
            Id = 7001,
            PublicId = "pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f",
            TenantId = 9,
            AuthorId = 1001,
            AuthorName = "alice",
            IsPublished = true,
            PublishTime = DateTime.UtcNow,
            IsEnabled = true,
            CollectCount = collectCount,
            CreateBy = "alice",
            CreateId = 1001
        }).ExecuteCommand();
    }

    private static SqlSugarScope CreateSqliteClient(string path) =>
        new(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

    private static string CreatePath(string scope) =>
        Path.Combine(
            Path.GetTempPath(),
            $"radish-post-bookmark-migration-{scope}-{Guid.NewGuid():N}.db");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
