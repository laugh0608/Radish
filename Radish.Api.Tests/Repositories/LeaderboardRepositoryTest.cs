using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Radish.Common.CoreTool;
using Radish.Common.HttpContextTool;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using Radish.Api.Tests.TestCollections;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

[Collection(PostgreSqlIntegrationCollection.CollectionName)]
public sealed class LeaderboardRepositoryTest : IDisposable
{
    private const string PostgreSqlConnectionStringEnvironmentVariable =
        "RADISH_TEST_POSTGRES_CONNECTION_STRING";
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        $"radish-leaderboard-{Guid.NewGuid():N}.db");
    private readonly SqlSugarScope _db;
    private readonly LeaderboardRepository _repository;
    private readonly DateTime _now = new(2026, 7, 30, 10, 0, 0, DateTimeKind.Utc);

    public LeaderboardRepositoryTest()
    {
        EnsurePublicTenantAppContext();
        _db = CreateSqliteScope(_path);
        InitializeSchema(_db);
        Seed(_db, _now);
        _repository = CreateRepository(_db);
    }

    [Fact]
    public async Task ExperienceRanking_ShouldApplyUserEligibilityFreezeAndStableTieOrder()
    {
        var (items, totalCount) = await _repository.GetExperienceRankingAsync(_now, 1, 20);

        Assert.Equal(4, totalCount);
        Assert.Equal(
            [1001L, 1002L, 1007L, 1005L],
            items.Select(item => item.UserId).ToArray());
        Assert.Equal([100L, 100L, 90L, 80L], items.Select(item => item.Value).ToArray());
        Assert.Equal(1, await _repository.GetUserExperienceRankAsync(1001, _now));
        Assert.Equal(2, await _repository.GetUserExperienceRankAsync(1002, _now));
        Assert.Equal(3, await _repository.GetUserExperienceRankAsync(1007, _now));
        Assert.Equal(0, await _repository.GetUserExperienceRankAsync(1003, _now));
        Assert.Equal(0, await _repository.GetUserExperienceRankAsync(1006, _now));

        var legacyPublicId = await _db.Queryable<User>()
            .Where(user => user.Id == 1005)
            .Select(user => user.PublicId)
            .FirstAsync(TestContext.Current.CancellationToken);
        var legacyPublicIndex = await _db.Queryable<User>()
            .Where(user => user.Id == 1005)
            .Select(user => user.PublicIndex)
            .FirstAsync(TestContext.Current.CancellationToken);
        Assert.Null(legacyPublicId);
        Assert.Null(legacyPublicIndex);
    }

    [Fact]
    public async Task ContentRankings_ShouldShareEligibilityTotalsAndOrdinalRanks()
    {
        var (postItems, postTotal) = await _repository.GetPostCountRankingAsync(1, 20);
        var (commentItems, commentTotal) = await _repository.GetCommentCountRankingAsync(1, 20);
        var (popularityItems, popularityTotal) = await _repository.GetPopularityRankingAsync(1, 20);

        Assert.Equal(3, postTotal);
        Assert.Equal(
            [1001L, 1002L, 1005L],
            postItems.Select(item => item.UserId).ToArray());
        Assert.Equal([2L, 2L, 1L], postItems.Select(item => item.Value).ToArray());
        Assert.Equal(1, await _repository.GetUserPostCountRankAsync(1001));
        Assert.Equal(2, await _repository.GetUserPostCountRankAsync(1002));
        Assert.Equal(0, await _repository.GetUserPostCountRankAsync(1003));

        Assert.Equal(2, commentTotal);
        Assert.Equal(
            [1005L, 1001L],
            commentItems.Select(item => item.UserId).ToArray());
        Assert.Equal([2L, 1L], commentItems.Select(item => item.Value).ToArray());
        Assert.Equal(1, await _repository.GetUserCommentCountRankAsync(1005));
        Assert.Equal(2, await _repository.GetUserCommentCountRankAsync(1001));

        Assert.Equal(3, popularityTotal);
        Assert.Equal(
            [1005L, 1001L, 1002L],
            popularityItems.Select(item => item.UserId).ToArray());
        Assert.Equal([10L, 7L, 5L], popularityItems.Select(item => item.Value).ToArray());
        Assert.Equal(1, await _repository.GetUserPopularityRankAsync(1005));
        Assert.Equal(2, await _repository.GetUserPopularityRankAsync(1001));
        Assert.Equal(0, await _repository.GetUserPopularityRankAsync(1003));
    }

    [Fact]
    public async Task HotProductRanking_ShouldFilterAvailabilityAndUseStableTieOrder()
    {
        var (items, totalCount) = await _repository.GetHotProductRankingAsync(1, 20);

        Assert.Equal(2, totalCount);
        Assert.Equal([5001L, 5002L], items.Select(item => item.Id).ToArray());
        Assert.All(items, item => Assert.Equal(20, item.SoldCount));
    }

    [Fact]
    public async Task EligibleUsers_ShouldNotBackfillPublicIdentityDuringRead()
    {
        var users = await _repository.GetEligibleUsersAsync([1005, 1003, 1004]);

        var user = Assert.Single(users);
        Assert.Equal(1005, user.UserId);
        Assert.Null(user.PublicId);
        Assert.Null(user.PublicIndex);

        var storedPublicId = await _db.Queryable<User>()
            .Where(item => item.Id == 1005)
            .Select(item => item.PublicId)
            .FirstAsync(TestContext.Current.CancellationToken);
        var storedPublicIndex = await _db.Queryable<User>()
            .Where(item => item.Id == 1005)
            .Select(item => item.PublicIndex)
            .FirstAsync(TestContext.Current.CancellationToken);
        Assert.Null(storedPublicId);
        Assert.Null(storedPublicIndex);
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task Repository_ShouldTranslateEligibilityAndAggregateQueriesOnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过排行榜 PostgreSQL 测试");

        var schema = $"leaderboard_{Guid.NewGuid():N}";
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
            InitializeSchema(db);
            Seed(db, _now);
            var repository = CreateRepository(db);

            var (experienceItems, experienceTotal) =
                await repository.GetExperienceRankingAsync(_now, 1, 20);
            var (postItems, postTotal) = await repository.GetPostCountRankingAsync(1, 20);
            var (popularityItems, popularityTotal) =
                await repository.GetPopularityRankingAsync(1, 20);

            Assert.Equal(
                [1001L, 1002L, 1007L, 1005L],
                experienceItems.Select(item => item.UserId).ToArray());
            Assert.Equal(4, experienceTotal);
            Assert.Equal(3, postTotal);
            Assert.Equal(3, popularityTotal);
            Assert.Equal(1001, experienceItems[0].UserId);
            Assert.Equal(1001, postItems[0].UserId);
            Assert.Equal(1005, popularityItems[0].UserId);
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync(
                $"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
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

    private static LeaderboardRepository CreateRepository(SqlSugarScope db)
    {
        return new LeaderboardRepository(
            new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));
    }

    private static void EnsurePublicTenantAppContext()
    {
        var currentUserAccessor = new Mock<ICurrentUserAccessor>(MockBehavior.Strict);
        currentUserAccessor
            .SetupGet(accessor => accessor.Current)
            .Returns(CurrentUser.Anonymous);
        var services = new ServiceCollection();
        services.AddSingleton(currentUserAccessor.Object);
        services.ConfigureApplication();
        services.BuildServiceProvider().ConfigureApplication();
        App.IsBuild = true;
    }

    private static SqlSugarScope CreateSqliteScope(string path)
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={path};Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    private static void InitializeSchema(SqlSugarScope db)
    {
        db.CodeFirst.InitTables<User, UserExperience, Post, Comment, Product>();
    }

    private static void Seed(SqlSugarScope db, DateTime now)
    {
        db.Insertable(new List<User>
        {
            CreateUser(1001, true, false, 1001),
            CreateUser(1002, true, false, 1002),
            CreateUser(1003, false, false, 1003),
            CreateUser(1004, true, true, 1004),
            CreateUser(1005, true, false, null),
            CreateUser(1006, true, false, 1006),
            CreateUser(1007, true, false, 1007)
        }).ExecuteCommand();
        db.Updateable<User>()
            .SetColumns(user => new User
            {
                PublicId = null,
                PublicIndex = null
            })
            .Where(user => user.Id == 1005)
            .ExecuteCommand();

        db.Insertable(new List<UserExperience>
        {
            CreateExperience(2001, 1001, 100),
            CreateExperience(2002, 1002, 100),
            CreateExperience(2003, 1003, 999),
            CreateExperience(2004, 1004, 998),
            CreateExperience(2005, 1005, 80),
            CreateExperience(2006, 1006, 500, true, null),
            CreateExperience(2007, 1007, 90, true, now.AddMinutes(-1))
        }).ExecuteCommand();

        db.Insertable(new List<Post>
        {
            CreatePost(3001, 1001, true, true, false, 4),
            CreatePost(3002, 1001, true, true, false, 1),
            CreatePost(3003, 1002, true, true, false, 5),
            CreatePost(3004, 1002, true, true, false, 0),
            CreatePost(3005, 1005, true, true, false, 0),
            CreatePost(3006, 1003, true, true, false, 100),
            CreatePost(3007, 1004, true, true, false, 100),
            CreatePost(3008, 1001, false, true, false, 100),
            CreatePost(3009, 1001, true, false, false, 100),
            CreatePost(3010, 1001, true, true, true, 100)
        }).ExecuteCommand();

        db.Insertable(new List<Comment>
        {
            CreateComment(4001, 1001, true, false, 2),
            CreateComment(4002, 1005, true, false, 6),
            CreateComment(4003, 1005, true, false, 4),
            CreateComment(4004, 1003, true, false, 100),
            CreateComment(4005, 1001, false, false, 100),
            CreateComment(4006, 1001, true, true, 100)
        }).ExecuteCommand();

        db.Insertable(new List<Product>
        {
            CreateProduct(5001, 20, true, true, false),
            CreateProduct(5002, 20, true, true, false),
            CreateProduct(5003, 100, false, true, false),
            CreateProduct(5004, 100, true, false, false),
            CreateProduct(5005, 100, true, true, true),
            CreateProduct(5006, 0, true, true, false)
        }).ExecuteCommand();
    }

    private static User CreateUser(
        long id,
        bool isEnabled,
        bool isDeleted,
        long? publicIndex)
    {
        return new User
        {
            Id = id,
            UserName = $"User-{id}",
            UserEmail = $"user-{id}@example.test",
            LoginPassword = "not-used",
            PublicId = publicIndex.HasValue ? $"usr_{id:D32}" : null,
            PublicIndex = publicIndex,
            IsEnable = isEnabled,
            IsDeleted = isDeleted,
            TenantId = 0
        };
    }

    private static UserExperience CreateExperience(
        long id,
        long userId,
        long totalExp,
        bool expFrozen = false,
        DateTime? frozenUntil = null)
    {
        return new UserExperience
        {
            Id = id,
            UserId = userId,
            TotalExp = totalExp,
            CurrentLevel = (int)(totalExp / 100),
            ExpFrozen = expFrozen,
            FrozenUntil = frozenUntil,
            IsDeleted = false,
            TenantId = 0
        };
    }

    private static Post CreatePost(
        long id,
        long authorId,
        bool isPublished,
        bool isEnabled,
        bool isDeleted,
        int likeCount)
    {
        return new Post
        {
            Id = id,
            Title = $"Post {id}",
            Content = "Content",
            AuthorId = authorId,
            AuthorName = $"User-{authorId}",
            IsPublished = isPublished,
            PublishTime = new DateTime(2026, 7, 30, 9, 0, 0, DateTimeKind.Local),
            IsEnabled = isEnabled,
            IsDeleted = isDeleted,
            LikeCount = likeCount,
            TenantId = 0
        };
    }

    private static Comment CreateComment(
        long id,
        long authorId,
        bool isEnabled,
        bool isDeleted,
        int likeCount)
    {
        return new Comment
        {
            Id = id,
            Content = $"Comment {id}",
            PostId = 3001,
            AuthorId = authorId,
            AuthorName = $"User-{authorId}",
            IsEnabled = isEnabled,
            IsDeleted = isDeleted,
            LikeCount = likeCount,
            TenantId = 0
        };
    }

    private static Product CreateProduct(
        long id,
        int soldCount,
        bool isOnSale,
        bool isEnabled,
        bool isDeleted)
    {
        return new Product
        {
            Id = id,
            Name = $"Product {id}",
            CategoryId = "test",
            Price = 10,
            SoldCount = soldCount,
            IsOnSale = isOnSale,
            IsEnabled = isEnabled,
            IsDeleted = isDeleted,
            TenantId = 0
        };
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
