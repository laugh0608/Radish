using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using Radish.Api.Tests.TestCollections;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

[Collection(PostgreSqlIntegrationCollection.CollectionName)]
public sealed class UserPostBookmarkRepositoryTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable =
        "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public async Task SetStateAsync_ShouldRemainIdempotentAndReuseRelationshipOnSqlite()
    {
        var path = CreatePath("state");
        using var db = CreateSqliteClient(path);
        try
        {
            InitializeSchema(db);
            SeedUserAndPost(db);
            var repository = CreateRepository(db);
            var firstAt = new DateTime(2026, 7, 29, 1, 0, 0, DateTimeKind.Utc);

            var created = await repository.SetStateAsync(Command(true, firstAt));
            var repeated = await repository.SetStateAsync(Command(true, firstAt.AddMinutes(1)));
            var removed = await repository.SetStateAsync(Command(false, firstAt.AddMinutes(2)));
            var repeatedRemoval = await repository.SetStateAsync(
                Command(false, firstAt.AddMinutes(3)));
            var restored = await repository.SetStateAsync(Command(true, firstAt.AddMinutes(4)));

            Assert.True(created.Changed);
            Assert.False(repeated.Changed);
            Assert.True(removed.Changed);
            Assert.False(repeatedRemoval.Changed);
            Assert.True(restored.Changed);
            Assert.True(restored.IsBookmarked);
            Assert.Equal(1, restored.CollectCount);

            var bookmark = Assert.Single(db.Queryable<UserPostBookmark>().ToList());
            Assert.Equal(created.Bookmark!.PublicId, bookmark.PublicId);
            Assert.Equal(firstAt.AddMinutes(4), DateTime.SpecifyKind(
                bookmark.BookmarkedAt,
                DateTimeKind.Utc));
            Assert.False(bookmark.IsDeleted);
            Assert.Equal(1, db.Queryable<Post>().InSingle(7001).CollectCount);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task SetStateAsync_ShouldSerializeConcurrentFirstBookmarkOnSqlite()
    {
        var path = CreatePath("concurrent");
        using var setupDb = CreateSqliteClient(path);
        using var firstDb = CreateSqliteClient(path);
        using var secondDb = CreateSqliteClient(path);
        try
        {
            InitializeSchema(setupDb);
            SeedUserAndPost(setupDb);
            var nowUtc = DateTime.UtcNow;

            var results = await Task.WhenAll(
                CreateRepository(firstDb).SetStateAsync(Command(true, nowUtc)),
                CreateRepository(secondDb).SetStateAsync(Command(true, nowUtc)));

            Assert.Single(results, result => result.Changed);
            Assert.All(results, result => Assert.True(result.IsBookmarked));
            Assert.Single(setupDb.Queryable<UserPostBookmark>().ToList());
            Assert.Equal(1, setupDb.Queryable<Post>().InSingle(7001).CollectCount);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveOwnedBookmarkWhenPostIsUnavailable()
    {
        var path = CreatePath("remove");
        using var db = CreateSqliteClient(path);
        try
        {
            InitializeSchema(db);
            SeedUserAndPost(db);
            var repository = CreateRepository(db);
            var created = await repository.SetStateAsync(Command(true, DateTime.UtcNow));
            db.Updateable<Post>()
                .SetColumns(item => new Post { IsDeleted = true })
                .Where(item => item.Id == 7001)
                .ExecuteCommand();

            var removed = await repository.RemoveAsync(
                9,
                1001,
                created.Bookmark!.PublicId,
                "alice",
                DateTime.UtcNow.AddMinutes(1));
            var repeated = await repository.RemoveAsync(
                9,
                1001,
                created.Bookmark.PublicId,
                "alice",
                DateTime.UtcNow.AddMinutes(2));

            Assert.True(removed.Changed);
            Assert.False(repeated.Changed);
            Assert.True(Assert.Single(db.Queryable<UserPostBookmark>().ToList()).IsDeleted);
            Assert.Equal(0, db.Queryable<Post>().InSingle(7001).CollectCount);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task SetStateAsync_ShouldSerializeConcurrentFirstBookmarkOnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过帖子收藏 PostgreSQL 并发测试");

        var schema = $"post_bookmark_{Guid.NewGuid():N}";
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
            using var setupDb = CreatePostgreSqlClient(connectionString);
            using var firstDb = CreatePostgreSqlClient(connectionString);
            using var secondDb = CreatePostgreSqlClient(connectionString);
            InitializeSchema(setupDb);
            SeedUserAndPost(setupDb);
            var nowUtc = DateTime.UtcNow;

            var results = await Task.WhenAll(
                CreateRepository(firstDb).SetStateAsync(Command(true, nowUtc)),
                CreateRepository(secondDb).SetStateAsync(Command(true, nowUtc)));

            Assert.Single(results, result => result.Changed);
            Assert.Single(setupDb.Queryable<UserPostBookmark>().ToList());
            Assert.Equal(1, setupDb.Queryable<Post>().InSingle(7001).CollectCount);
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    private static PostBookmarkStateCommand Command(bool isBookmarked, DateTime nowUtc) =>
        new(9, 1001, 7001, "alice", isBookmarked, nowUtc);

    private static UserPostBookmarkRepository CreateRepository(SqlSugarScope db) =>
        new(new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));

    private static void InitializeSchema(SqlSugarScope db)
    {
        db.CodeFirst.InitTables<User>();
        db.CodeFirst.InitTables<Post>();
        db.CodeFirst.InitTables<UserPostBookmark>();
    }

    private static void SeedUserAndPost(SqlSugarScope db)
    {
        db.Insertable(new User
        {
            Id = 1001,
            PublicId = "usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f",
            PublicIndex = 1001,
            UserName = "alice",
            UserEmail = "alice@bookmark.test",
            LoginPassword = "hash",
            TenantId = 9,
            IsEnable = true,
            IsDeleted = false
        }).ExecuteCommand();
        db.Insertable(new Post("收藏测试帖子", "用于验证收藏权威关系的正文")
        {
            Id = 7001,
            PublicId = "pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f",
            TenantId = 9,
            AuthorId = 1001,
            AuthorName = "alice",
            IsPublished = true,
            PublishTime = DateTime.UtcNow,
            IsEnabled = true,
            CreateBy = "alice",
            CreateId = 1001
        }).ExecuteCommand();
    }

    private static SqlSugarScope CreateSqliteClient(string path) =>
        new(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={path};Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

    private static SqlSugarScope CreatePostgreSqlClient(string connectionString) =>
        PostgreSqlIntegrationSqlSugarFactory.CreateScope(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = connectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

    private static string CreatePath(string scope) =>
        Path.Combine(
            Path.GetTempPath(),
            $"radish-post-bookmark-{scope}-{Guid.NewGuid():N}.db");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
