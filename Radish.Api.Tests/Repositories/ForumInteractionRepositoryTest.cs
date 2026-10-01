using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Api.Tests.TestCollections;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

[Collection(PostgreSqlIntegrationCollection.CollectionName)]
public sealed class ForumInteractionRepositoryTest
{
    [Fact]
    public async Task InteractorsAndLikes_ShouldWorkOnSqlite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-forum-interactors-{Guid.NewGuid():N}.db");
        using var db = new SqlSugarScope(Config(DbType.Sqlite, $"Data Source={path}"));
        try
        {
            await VerifyInteractionsAsync(db);
        }
        finally
        {
            db.Close();
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task InteractorsAndLikes_ShouldWorkWithProductionLowercasePostgresNames()
    {
        var connection = Environment.GetEnvironmentVariable("RADISH_TEST_POSTGRES_CONNECTION_STRING");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "未配置 PostgreSQL 测试连接");
        var schema = $"forum_interactors_{Guid.NewGuid():N}";
        using var admin = PostgreSqlIntegrationSqlSugarFactory.CreateClient(Config(DbType.PostgreSQL, connection!));
        await admin.Ado.ExecuteCommandAsync($"CREATE SCHEMA \"{schema}\"");
        try
        {
            using var db = PostgreSqlIntegrationSqlSugarFactory.CreateScope(Config(DbType.PostgreSQL,
                $"{connection!.TrimEnd(';')};Search Path={schema};Pooling=false"));
            await VerifyInteractionsAsync(db);
            Assert.Contains(db.DbMaintenance.GetTableInfoList(false), table => table.Name == "comment");
        }
        finally
        {
            await admin.Ado.ExecuteCommandAsync($"DROP SCHEMA \"{schema}\" CASCADE");
        }
    }

    private static ConnectionConfig Config(DbType type, string connection) => new()
    {
        ConfigId = "main", DbType = type, ConnectionString = connection,
        IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute
    };

    private static async Task VerifyInteractionsAsync(SqlSugarScope db)
    {
        db.CodeFirst.InitTables<Post, Comment, UserPostLike, UserCommentLike, ReliableOutboxMessage>();
        var unitOfWork = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
        var comments = new CommentRepository(unitOfWork, new ReliableOutboxRepository(db));
        var posts = new PostRepository(unitOfWork, new ReliableOutboxRepository(db));
        var authors = new Dictionary<long, long> { [1001] = 2001, [1002] = 2002 };
        // 首篇帖子尚无评论时，同样会执行互动人 SQL。
        Assert.Empty(await comments.QueryLatestInteractorCommentsByPostIdsAsync(authors, 3));
        var now = DateTime.UtcNow;
        db.Insertable(new Post("标题", "正文")
        {
            Id = 1001, AuthorId = 2001, IsPublished = true, PublishTime = now,
            TenantId = 0, CreateTime = now
        }).ExecuteCommand();
        var rows = new[]
        {
            Comment(1, 1001, 3001, now), Comment(2, 1001, 3001, now.AddSeconds(1)),
            Comment(3, 1001, 3002, now.AddSeconds(1)), Comment(4, 1001, 3003, now.AddSeconds(2)),
            Comment(5, 1001, 3004, now.AddSeconds(3)), Comment(6, 1001, 2001, now.AddSeconds(4)),
            Comment(7, 1001, 3005, now.AddSeconds(5), deleted: true),
            Comment(8, 1001, 3006, now.AddSeconds(6), enabled: false),
            Comment(9, 1001, 3007, now.AddSeconds(7), tenant: 9),
            Comment(10, 1002, 3008, now.AddSeconds(8))
        };
        db.Insertable(rows).ExecuteCommand();
        var result = await comments.QueryLatestInteractorCommentsByPostIdsAsync(authors, 3);
        Assert.Equal(new long[] { 5, 4, 3, 10 }, result.Select(item => item.Id));
        Assert.Empty(await comments.QueryLatestInteractorCommentsByPostIdsAsync(new Dictionary<long, long>(), 3));
        Assert.Empty(await comments.QueryLatestInteractorCommentsByPostIdsAsync(authors, 0));

        Assert.Equal(1, (await posts.TogglePostLikeAsync(4001, "liker", 1001)).LikeCount);
        Assert.Equal(0, (await posts.TogglePostLikeAsync(4001, "liker", 1001)).LikeCount);
        Assert.Equal(1, (await comments.ToggleCommentLikeAsync(4001, "liker", 1)).LikeCount);
        Assert.Equal(0, (await comments.ToggleCommentLikeAsync(4001, "liker", 1)).LikeCount);
    }

    private static Comment Comment(long id, long post, long author, DateTime created,
        bool deleted = false, bool enabled = true, long tenant = 0) => new("评论")
    {
        Id = id, PostId = post, AuthorId = author, CreateTime = created,
        IsDeleted = deleted, IsEnabled = enabled, TenantId = tenant
    };
}
