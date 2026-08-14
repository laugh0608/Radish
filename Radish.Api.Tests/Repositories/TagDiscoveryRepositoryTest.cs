using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Radish.Common.HttpContextTool;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class TagDiscoveryRepositoryTest : IDisposable
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        $"radish-tag-discovery-{Guid.NewGuid():N}.db");
    private readonly SqlSugarScope _db;
    private readonly TagDiscoveryRepository _repository;

    public TagDiscoveryRepositoryTest()
    {
        _db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={_path};Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        _db.CodeFirst.InitTables<Tag, Post, PostTag>();
        _repository = new TagDiscoveryRepository(
            new UnitOfWorkManage(_db, NullLogger<UnitOfWorkManage>.Instance),
            CreateCurrentUserAccessor());
        Seed(_db);
    }

    private static ICurrentUserAccessor CreateCurrentUserAccessor()
    {
        var accessor = new Mock<ICurrentUserAccessor>(MockBehavior.Strict);
        accessor.SetupGet(item => item.Current).Returns(CurrentUser.Anonymous);
        return accessor.Object;
    }

    [Fact]
    public async Task Repository_Should_Use_Public_Visibility_For_Counts_And_Hot_Tags()
    {
        var publicPostCount = await _repository.QueryPublicPostCountAsync(1001);
        var hotTags = await _repository.QueryHotPublicTagsAsync(20);

        Assert.Equal(2, publicPostCount);
        Assert.Collection(
            hotTags,
            tag =>
            {
                Assert.Equal(1001, tag.Id);
                Assert.Equal(2, tag.PostCount);
            },
            tag =>
            {
                Assert.Equal(1002, tag.Id);
                Assert.Equal(1, tag.PostCount);
            });
        Assert.DoesNotContain(hotTags, tag => tag.Id is 1003 or 1004);
    }

    [Fact]
    public async Task Repository_Should_Query_Related_And_Indexable_Tags_In_Database()
    {
        var relatedTags = await _repository.QueryRelatedPublicTagsAsync(1001, 8);
        var indexableCount = await _repository.QueryIndexableTagCountAsync();
        var indexableTags = await _repository.QueryIndexableTagPageAsync(1, 100);

        var relatedTag = Assert.Single(relatedTags);
        Assert.Equal(1002, relatedTag.Id);
        Assert.Equal(1, relatedTag.PostCount);
        Assert.Equal(2, indexableCount);
        Assert.Equal([1001L, 1002L], indexableTags.Select(tag => tag.Id).Order().ToArray());
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task Repository_Should_Translate_Public_Tag_Aggregates_On_PostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过标签发现 PostgreSQL 测试");

        var schema = $"tag_discovery_{Guid.NewGuid():N}";
        using var adminDb = PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = adminConnectionString!,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = PostgreSqlIntegrationSqlSugarFactory.CreateScope(new ConnectionConfig
            {
                ConfigId = "main",
                ConnectionString = connectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
            db.CodeFirst.InitTables<Tag, Post, PostTag>();
            Seed(db);
            var repository = new TagDiscoveryRepository(
                new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance),
                CreateCurrentUserAccessor());

            var hotTags = await repository.QueryHotPublicTagsAsync(20);
            var relatedTags = await repository.QueryRelatedPublicTagsAsync(1001, 8);

            Assert.Equal([1001L, 1002L], hotTags.Select(tag => tag.Id).ToArray());
            Assert.Equal(1002, Assert.Single(relatedTags).Id);
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private static void Seed(SqlSugarScope db)
    {
        var now = new DateTime(2026, 7, 29, 8, 0, 0, DateTimeKind.Utc);
        db.Insertable(new List<Tag>
        {
            CreateTag(1001, "C#", "csharp", 1, true),
            CreateTag(1002, "ASP.NET", "aspnet", 2, true),
            CreateTag(1003, "已禁用", "disabled", 3, false),
            CreateTag(1004, "空标签", "empty", 4, true)
        }).ExecuteCommand();
        db.Insertable(new List<Post>
        {
            CreatePost(2001, true, true, false, now.AddHours(1)),
            CreatePost(2002, true, true, false, now.AddHours(2)),
            CreatePost(2003, false, true, false, now.AddHours(3)),
            CreatePost(2004, true, false, false, now.AddHours(4)),
            CreatePost(2005, true, true, true, now.AddHours(5))
        }).ExecuteCommand();
        db.Insertable(new List<PostTag>
        {
            CreatePostTag(3001, 2001, 1001, now.AddHours(1)),
            CreatePostTag(3008, 2001, 1001, now.AddHours(1)),
            CreatePostTag(3002, 2001, 1002, now.AddHours(1)),
            CreatePostTag(3003, 2001, 1003, now.AddHours(1)),
            CreatePostTag(3004, 2002, 1001, now.AddHours(2)),
            CreatePostTag(3005, 2003, 1001, now.AddHours(3)),
            CreatePostTag(3006, 2004, 1001, now.AddHours(4)),
            CreatePostTag(3007, 2005, 1001, now.AddHours(5))
        }).ExecuteCommand();
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static Tag CreateTag(long id, string name, string slug, int sortOrder, bool isEnabled)
    {
        return new Tag(name)
        {
            Id = id,
            Slug = slug,
            SortOrder = sortOrder,
            IsEnabled = isEnabled,
            IsDeleted = false
        };
    }

    private static Post CreatePost(
        long id,
        bool isPublished,
        bool isEnabled,
        bool isDeleted,
        DateTime createTime)
    {
        return new Post(new PostInitializationOptions($"帖子 {id}", "测试正文")
        {
            AuthorId = 9001,
            AuthorName = "Tester",
            TenantId = 0,
            IsPublished = isPublished,
            IsEnabled = isEnabled
        })
        {
            Id = id,
            IsDeleted = isDeleted,
            PublishTime = createTime,
            CreateTime = createTime,
            ModifyTime = createTime
        };
    }

    private static PostTag CreatePostTag(long id, long postId, long tagId, DateTime createTime)
    {
        return new PostTag(postId, tagId)
        {
            Id = id,
            CreateTime = createTime
        };
    }
}
