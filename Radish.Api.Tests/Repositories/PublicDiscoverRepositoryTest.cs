using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.IRepository;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class PublicDiscoverRepositoryTest
{
    private static readonly DateTime CutoffUtc = new(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MainSources_ShouldApplyCurrentEligibilityAndExcludeQuestionsFromPosts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-public-discover-main-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteScope("main", path);

        try
        {
            db.CodeFirst.InitTables<User, WikiDocument, Post, PostQuestion, Comment>();
            db.CodeFirst.InitTables<CommentHighlight>();
            db.Insertable(CreateUser(1001, 1)).ExecuteCommand();
            db.Insertable(CreateWiki(2001, 1001, CutoffUtc.AddHours(-2))).ExecuteCommand();
            db.Insertable(new[]
            {
                CreatePost(3001, 1001, 1, CutoffUtc.AddHours(-1)),
                CreatePost(3002, 1001, 2, CutoffUtc.AddMinutes(-90)),
                CreatePost(3003, 1001, 3, CutoffUtc.AddHours(-3)),
                CreatePost(3004, 1001, 4, CutoffUtc.AddMinutes(10))
            }).ExecuteCommand();
            db.Insertable(new PostQuestion
            {
                Id = 4001,
                TenantId = 0,
                PostId = 3002,
                AnswerCount = 3,
                CreateTime = CutoffUtc.AddMinutes(-90),
                CreateBy = "Author"
            }).ExecuteCommand();
            db.Insertable(new Comment
            {
                Id = 5001,
                TenantId = 0,
                PostId = 3003,
                AuthorId = 1001,
                AuthorName = "Author",
                Content = "**这条评论** <em>值得讨论</em>",
                LikeCount = 8,
                IsEnabled = true,
                CreateTime = CutoffUtc.AddHours(-1),
                CreateBy = "Author"
            }).ExecuteCommand();
            db.Insertable(new CommentHighlight
            {
                Id = 6001,
                TenantId = 0,
                PostId = 3003,
                CommentId = 5001,
                HighlightType = 1,
                IsCurrent = true,
                LikeCount = 8,
                AuthorId = 1001,
                AuthorName = "Author",
                StatDate = CutoffUtc.Date,
                CreateTime = CutoffUtc.AddMinutes(-30),
                CreateBy = "System"
            }).ExecuteCommand();
            var repository = CreateMainRepository(db);
            var window = new PublicDiscoverSourceWindow(0, CutoffUtc, null, null, null, 20);

            var memberActivities = await repository.QueryMemberActivitiesAsync(window);
            var highlights = await repository.QueryHighlightedCommentsAsync(window);
            var posts = await repository.QueryPostsAsync(window);
            var questions = await repository.QueryQuestionsAsync(window);
            var pulse = await repository.QueryPulseAsync(0, CutoffUtc.AddHours(-24), CutoffUtc);

            Assert.Single(memberActivities);
            Assert.Equal(PublicDiscoverItemKind.MemberActivity, memberActivities[0].Kind);
            Assert.Single(highlights);
            Assert.Equal(5001, highlights[0].CommentId);
            Assert.Equal(8, highlights[0].MetricValue);
            Assert.Equal(new long[] { 3001, 3003 }, posts.Select(item => item.SourceId));
            Assert.DoesNotContain(posts, item => item.SourceId == 3002);
            Assert.Single(questions);
            Assert.Equal(4001, questions[0].SourceId);
            Assert.Equal(3, questions[0].MetricValue);
            Assert.Equal(5, pulse.EligibleItemCount);
            Assert.Equal(1, pulse.KnowledgeContributionCount);
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
    public async Task ChannelSource_ShouldReturnOnlyExplicitSafeSummariesAndDatabasePulse()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-public-discover-chat-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteScope("chat", path);

        try
        {
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<Channel, ChannelMessage, ChannelDiscoverVisibilityEvent>();
            chatDb.Insertable(new[]
            {
                CreateChannel(7001, "general", ChannelDiscoverVisibility.Summary, CutoffUtc.AddHours(-1)),
                CreateChannel(7002, "hidden", ChannelDiscoverVisibility.Hidden, CutoffUtc.AddMinutes(-30)),
                CreateChannel(7003, "invalid_slug", ChannelDiscoverVisibility.Summary, CutoffUtc.AddMinutes(-20))
            }).ExecuteCommand();
            chatDb.Insertable(new[]
            {
                CreateMessage(8001, 7001, CutoffUtc.AddHours(-2), isDeleted: false),
                CreateMessage(8002, 7001, CutoffUtc.AddHours(-3), isDeleted: false),
                CreateMessage(8003, 7001, CutoffUtc.AddHours(-4), isDeleted: true)
            }).ExecuteCommand();
            var repository = CreateChannelRepository(db);
            var window = new PublicDiscoverSourceWindow(0, CutoffUtc, null, null, null, 20);

            var items = await repository.QueryChannelSummariesAsync(window, CutoffUtc.AddHours(-24));
            var pulse = await repository.QueryPulseAsync(0, CutoffUtc.AddHours(-24), CutoffUtc);

            Assert.Single(items);
            Assert.Equal(7001, items[0].SourceId);
            Assert.Equal(PublicDiscoverItemKind.ChannelSummary, items[0].Kind);
            Assert.Equal(2, items[0].MetricValue);
            Assert.Equal(1, pulse.DiscoverableChannelCount);
            Assert.Equal(1, pulse.RecentChannelItemCount);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static PublicDiscoverRepository CreateMainRepository(SqlSugarScope db) => new(
        new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));

    private static ChannelDiscoverabilityRepository CreateChannelRepository(SqlSugarScope db) => new(
        new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));

    private static SqlSugarScope CreateSqliteScope(string configId, string path) => new(new ConnectionConfig
    {
        ConfigId = configId,
        ConnectionString = $"Data Source={path}",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute
    });

    private static User CreateUser(long id, int suffix) => new()
    {
        Id = id,
        TenantId = 0,
        PublicId = $"usr_{suffix.ToString("x32")}",
        UserName = "Author",
        IsEnable = true,
        IsDeleted = false,
        CreateTime = CutoffUtc.AddDays(-10)
    };

    private static WikiDocument CreateWiki(long id, long ownerUserId, DateTime publishedAtUtc) => new()
    {
        Id = id,
        TenantId = 0,
        Title = "统一公开读模型",
        Slug = "public-read-model",
        Summary = "介绍公开发现的资格边界。",
        MarkdownContent = "# 统一公开读模型",
        Status = (int)WikiDocumentStatusEnum.Published,
        Visibility = (int)WikiDocumentVisibilityEnum.Public,
        OwnerUserId = ownerUserId,
        PublishedAt = publishedAtUtc,
        CreateTime = publishedAtUtc.AddDays(-1),
        CreateBy = "Author"
    };

    private static Post CreatePost(long id, long authorId, int suffix, DateTime publishedAtUtc) => new()
    {
        Id = id,
        TenantId = 0,
        PublicId = $"pst_{suffix.ToString("x32")}",
        Title = $"公开帖子 {suffix}",
        Slug = $"public-post-{suffix}",
        Summary = "帖子摘要",
        Content = "帖子正文",
        AuthorId = authorId,
        AuthorName = "Author",
        IsPublished = true,
        IsEnabled = true,
        IsDeleted = false,
        PublishTime = publishedAtUtc,
        CreateTime = publishedAtUtc.AddMinutes(-5),
        CreateBy = "Author",
        CommentCount = suffix
    };

    private static Channel CreateChannel(
        long id,
        string slug,
        ChannelDiscoverVisibility visibility,
        DateTime lastMessageTimeUtc) => new()
    {
        Id = id,
        TenantId = 0,
        Name = slug,
        Slug = slug,
        Type = ChannelType.Public,
        IsEnabled = true,
        DiscoverVisibility = visibility,
        DiscoverVisibilityVersion = visibility == ChannelDiscoverVisibility.Summary ? 1 : 0,
        LastMessageTime = lastMessageTimeUtc,
        CreateTime = lastMessageTimeUtc.AddDays(-10),
        CreateBy = "System"
    };

    private static ChannelMessage CreateMessage(
        long id,
        long channelId,
        DateTime createTimeUtc,
        bool isDeleted) => new()
    {
        Id = id,
        TenantId = 0,
        ChannelId = channelId,
        UserId = 1001,
        UserName = "Author",
        Type = MessageType.Text,
        Content = "不得进入公开投影的消息正文",
        CreateTime = createTimeUtc,
        IsDeleted = isDeleted
    };
}
