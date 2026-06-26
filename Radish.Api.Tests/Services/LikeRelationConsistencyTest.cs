using System;
using System.IO;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radish.Common.CacheTool;
using Radish.Common.CoreTool;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using Radish.Service;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class LikeRelationConsistencyTest
{
    [Fact]
    public async Task TogglePostLikeAsync_ShouldReuseSingleRelation_AndUpdateCount()
    {
        using var harness = LikeRepositoryHarness.Create();
        harness.Db.Insertable(new Post("标题", "正文")
        {
            Id = 1001,
            AuthorId = 2001,
            IsPublished = true,
            PublishTime = DateTime.UtcNow,
            TenantId = 0,
            LikeCount = 0
        }).ExecuteCommand();

        var liked = await harness.PostRepository.TogglePostLikeAsync(3001, 1001);
        var unliked = await harness.PostRepository.TogglePostLikeAsync(3001, 1001);
        var restored = await harness.PostRepository.TogglePostLikeAsync(3001, 1001);

        Assert.True(liked.IsLiked);
        Assert.Equal(1, liked.Delta);
        Assert.Equal(1, liked.LikeCount);
        Assert.False(unliked.IsLiked);
        Assert.Equal(-1, unliked.Delta);
        Assert.Equal(0, unliked.LikeCount);
        Assert.True(restored.IsLiked);
        Assert.Equal(1, restored.Delta);
        Assert.Equal(1, restored.LikeCount);

        var relations = harness.Db.Queryable<UserPostLike>()
            .Where(like => like.UserId == 3001 && like.PostId == 1001)
            .ToList();
        var post = harness.Db.Queryable<Post>().First(post => post.Id == 1001);

        Assert.Single(relations);
        Assert.False(relations[0].IsDeleted);
        Assert.Equal(1, post.LikeCount);
    }

    [Fact]
    public async Task ToggleCommentLikeAsync_ShouldNotMakeCountNegative_WhenCancellingStaleActiveRelation()
    {
        using var harness = LikeRepositoryHarness.Create();
        harness.Db.Insertable(new Comment("评论")
        {
            Id = 4001,
            PostId = 1001,
            AuthorId = 2001,
            TenantId = 0,
            LikeCount = 0
        }).ExecuteCommand();
        harness.Db.Insertable(new UserCommentLike
        {
            Id = 5001,
            TenantId = 0,
            UserId = 3001,
            CommentId = 4001,
            PostId = 1001,
            IsDeleted = false,
            LikedAt = DateTime.UtcNow
        }).ExecuteCommand();

        var result = await harness.CommentRepository.ToggleCommentLikeAsync(3001, 4001);

        Assert.False(result.IsLiked);
        Assert.Equal(-1, result.Delta);
        Assert.Equal(0, result.LikeCount);

        var relation = harness.Db.Queryable<UserCommentLike>().First(like => like.Id == 5001);
        var comment = harness.Db.Queryable<Comment>().First(comment => comment.Id == 4001);

        Assert.True(relation.IsDeleted);
        Assert.Equal(0, comment.LikeCount);
    }

    [Fact]
    public async Task PostToggleLikeAsync_ShouldNotTriggerRewards_WhenRelationDidNotChange()
    {
        var customRepository = new Mock<IPostRepository>(MockBehavior.Strict);
        var coinRewardService = new Mock<ICoinRewardService>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var dedupService = new Mock<INotificationDedupService>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var service = CreatePostService(
            customRepository.Object,
            coinRewardService.Object,
            notificationService.Object,
            dedupService.Object,
            experienceService.Object);

        customRepository
            .Setup(repository => repository.TogglePostLikeAsync(3001, 1001))
            .ReturnsAsync(new PostLikePersistenceResult(
                1001,
                2001,
                "标题",
                "post_public",
                true,
                1,
                0));

        var result = await service.ToggleLikeAsync(3001, 1001);

        Assert.True(result.IsLiked);
        Assert.Equal(1, result.LikeCount);
        customRepository.VerifyAll();
        coinRewardService.VerifyNoOtherCalls();
        notificationService.VerifyNoOtherCalls();
        dedupService.VerifyNoOtherCalls();
        experienceService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CommentToggleLikeAsync_ShouldNotTriggerRewardsOrHighlight_WhenRelationDidNotChange()
    {
        var customRepository = new Mock<ICommentRepository>(MockBehavior.Strict);
        var postService = new Mock<IPostService>(MockBehavior.Strict);
        var coinRewardService = new Mock<ICoinRewardService>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var dedupService = new Mock<INotificationDedupService>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var service = CreateCommentService(
            customRepository.Object,
            postService.Object,
            coinRewardService.Object,
            notificationService.Object,
            dedupService.Object,
            experienceService.Object);

        customRepository
            .Setup(repository => repository.ToggleCommentLikeAsync(3001, 4001))
            .ReturnsAsync(new CommentLikePersistenceResult(
                4001,
                1001,
                null,
                2001,
                "评论",
                true,
                1,
                0));

        var result = await service.ToggleLikeAsync(3001, 4001);

        Assert.True(result.IsLiked);
        Assert.Equal(1, result.LikeCount);
        Assert.Null(result.HighlightRecheckResult);
        customRepository.VerifyAll();
        postService.VerifyNoOtherCalls();
        coinRewardService.VerifyNoOtherCalls();
        notificationService.VerifyNoOtherCalls();
        dedupService.VerifyNoOtherCalls();
        experienceService.VerifyNoOtherCalls();
    }

    private static PostService CreatePostService(
        IPostRepository postRepository,
        ICoinRewardService coinRewardService,
        INotificationService notificationService,
        INotificationDedupService dedupService,
        IExperienceService experienceService)
    {
        return new PostService(
            new Mock<IMapper>().Object,
            new Mock<IBaseRepository<Post>>().Object,
            new Mock<IBaseRepository<UserPostLike>>().Object,
            new Mock<IBaseRepository<PostTag>>().Object,
            new Mock<IBaseRepository<Category>>().Object,
            new Mock<IBaseRepository<Tag>>().Object,
            new Mock<IBaseRepository<PostPoll>>().Object,
            new Mock<IBaseRepository<PostPollOption>>().Object,
            new Mock<IBaseRepository<PostPollVote>>().Object,
            new Mock<IBaseRepository<PostQuestion>>().Object,
            new Mock<IBaseRepository<PostAnswer>>().Object,
            new Mock<ITagService>().Object,
            coinRewardService,
            notificationService,
            dedupService,
            experienceService,
            new Mock<IBaseRepository<PostEditHistory>>().Object,
            new Mock<IAttachmentService>().Object,
            Options.Create(new ForumEditHistoryOptions()),
            new Mock<ISystemSettingProvider>().Object,
            postCustomRepository: postRepository);
    }

    private static CommentService CreateCommentService(
        ICommentRepository commentRepository,
        IPostService postService,
        ICoinRewardService coinRewardService,
        INotificationService notificationService,
        INotificationDedupService dedupService,
        IExperienceService experienceService)
    {
        return new CommentService(
            new Mock<IMapper>().Object,
            new Mock<IBaseRepository<Comment>>().Object,
            new Mock<IBaseRepository<UserCommentLike>>().Object,
            new Mock<IBaseRepository<CommentHighlight>>().Object,
            postService,
            new Mock<ICaching>().Object,
            coinRewardService,
            notificationService,
            dedupService,
            experienceService,
            new Mock<IAttachmentUrlResolver>().Object,
            Options.Create(new CommentHighlightOptions()),
            new Mock<IBaseRepository<CommentEditHistory>>().Object,
            Options.Create(new ForumEditHistoryOptions()),
            new Mock<ISystemSettingProvider>().Object,
            commentCustomRepository: commentRepository);
    }

    private sealed class LikeRepositoryHarness : IDisposable
    {
        private static readonly object AppServicesLock = new();
        private static bool appServicesConfigured;
        private readonly string _dbPath;

        private LikeRepositoryHarness(string dbPath, SqlSugarScope db)
        {
            _dbPath = dbPath;
            Db = db;
            var unitOfWork = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
            PostRepository = new PostRepository(unitOfWork);
            CommentRepository = new CommentRepository(unitOfWork);
        }

        public SqlSugarScope Db { get; }

        public PostRepository PostRepository { get; }

        public CommentRepository CommentRepository { get; }

        public static LikeRepositoryHarness Create()
        {
            EnsureAppServices();
            var dbPath = Path.Combine(Path.GetTempPath(), $"radish-like-{Guid.NewGuid():N}.db");
            var db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "Main",
                DbType = DbType.Sqlite,
                ConnectionString = $"Data Source={dbPath}",
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });

            db.CodeFirst.InitTables<Post, Comment, UserPostLike, UserCommentLike>();
            return new LikeRepositoryHarness(dbPath, db);
        }

        private static void EnsureAppServices()
        {
            if (appServicesConfigured)
            {
                return;
            }

            lock (AppServicesLock)
            {
                if (appServicesConfigured)
                {
                    return;
                }

                var currentUserAccessor = new Mock<ICurrentUserAccessor>(MockBehavior.Strict);
                currentUserAccessor
                    .Setup(accessor => accessor.Current)
                    .Returns(CurrentUser.Anonymous);

                var services = new ServiceCollection();
                services.AddSingleton(currentUserAccessor.Object);
                services.ConfigureApplication();
                appServicesConfigured = true;
            }
        }

        public void Dispose()
        {
            Db.Dispose();
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
    }
}
