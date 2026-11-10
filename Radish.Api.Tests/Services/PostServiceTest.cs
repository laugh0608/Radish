using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.Exceptions;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public partial class PostServiceTest
{
    [Fact]
    public async Task FillPostAvatarAndInteractorsAsync_Should_Fill_AuthorAvatar()
    {
        var attachmentService = new Mock<IAttachmentService>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        attachmentService
            .Setup(service => service.GetLatestAvatarAssetMapAsync(It.Is<IReadOnlyCollection<long>>(userIds =>
                userIds.Contains(2001))))
            .ReturnsAsync(new Dictionary<long, AttachmentAssetDto>
            {
                [2001] = new()
                {
                    AttachmentId = 3001,
                    Url = "/_assets/attachments/3001",
                    ThumbnailUrl = "/_assets/attachments/3001/thumbnail"
                }
            });
        userRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<User, bool>>?>()))
            .ReturnsAsync(
            [
                new User(new UserInitializationOptions("luobo@example.com", "pwd")
                {
                    UserName = "luobo",
                    UserEmail = "luobo@example.com"
                })
                {
                    Id = 2001,
                    IsDeleted = false
                }
            ]);

        var service = new PostService(
            Mock.Of<IBaseRepository<Post>>(),
            Mock.Of<IBaseRepository<UserPostLike>>(),
            Mock.Of<IBaseRepository<PostTag>>(),
            Mock.Of<IBaseRepository<Category>>(),
            Mock.Of<IBaseRepository<Tag>>(),
            Mock.Of<IBaseRepository<PostPoll>>(),
            Mock.Of<IBaseRepository<PostPollOption>>(),
            Mock.Of<IBaseRepository<PostPollVote>>(),
            Mock.Of<IBaseRepository<PostQuestion>>(),
            Mock.Of<IBaseRepository<PostAnswer>>(),
            Mock.Of<ITagService>(),
            Mock.Of<ICoinRewardService>(),
            Mock.Of<INotificationService>(),
            Mock.Of<INotificationDedupService>(),
            Mock.Of<IExperienceService>(),
            Mock.Of<IBaseRepository<PostEditHistory>>(),
            attachmentService.Object,
            Options.Create(new ForumEditHistoryOptions()),
            CreateDefaultSystemSettingProvider(),
            userRepository: userRepository.Object);

        var posts = new List<PostVo>
        {
            new()
            {
                VoId = 1001,
                VoAuthorId = 2001,
                VoAuthorName = "luobo"
            }
        };

        await service.FillPostAvatarAndInteractorsAsync(posts);

        Assert.Equal("/_assets/attachments/3001", posts[0].VoAuthorAvatarUrl);
        Assert.Equal("luobo", posts[0].VoAuthorName);
        attachmentService.VerifyAll();
        userRepository.VerifyAll();
    }

    [Fact]
    public async Task FillPostListMetadataAsync_Should_BatchFill_Category_Tags_And_Poll_Summary()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var userPostLikeRepository = new Mock<IBaseRepository<UserPostLike>>(MockBehavior.Strict);
        var postTagRepository = new Mock<IBaseRepository<PostTag>>(MockBehavior.Strict);
        var categoryRepository = new Mock<IBaseRepository<Category>>(MockBehavior.Strict);
        var tagRepository = new Mock<IBaseRepository<Tag>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postQuestionRepository = new Mock<IBaseRepository<PostQuestion>>(MockBehavior.Strict);
        var postAnswerRepository = new Mock<IBaseRepository<PostAnswer>>(MockBehavior.Strict);
        var tagService = new Mock<ITagService>(MockBehavior.Strict);
        var coinRewardService = new Mock<ICoinRewardService>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var dedupService = new Mock<INotificationDedupService>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var postEditHistoryRepository = new Mock<IBaseRepository<PostEditHistory>>(MockBehavior.Strict);

        postTagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>?>()))
            .ReturnsAsync(
            [
                new PostTag(1001, 501),
                new PostTag(1001, 502)
            ]);
        categoryRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Category, bool>>?>()))
            .ReturnsAsync(
            [
                new Category("综合讨论")
                {
                    Id = 101,
                    IsEnabled = true,
                    IsDeleted = false
                }
            ]);
        tagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Tag, bool>>?>()))
            .ReturnsAsync(
            [
                new Tag("投票")
                {
                    Id = 501,
                    Slug = "poll",
                    IsEnabled = true,
                    IsDeleted = false
                },
                new Tag("社区")
                {
                    Id = 502,
                    Slug = "community",
                    IsEnabled = true,
                    IsDeleted = false
                }
            ]);
        postPollRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync(
            [
                new PostPoll
                {
                    Id = 2001,
                    PostId = 1001,
                    TotalVoteCount = 9,
                    IsClosed = false,
                    EndTime = DateTime.UtcNow.AddHours(2)
                }
            ]);
        postQuestionRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostQuestion, bool>>?>()))
            .ReturnsAsync(
            [
                new PostQuestion
                {
                    Id = 3001,
                    PostId = 1001,
                    IsSolved = true,
                    AnswerCount = 2,
                    AcceptedAnswerId = 4001,
                    IsDeleted = false
                }
            ]);

        var service = new PostService(
            postRepository.Object,
            userPostLikeRepository.Object,
            postTagRepository.Object,
            categoryRepository.Object,
            tagRepository.Object,
            postPollRepository.Object,
            postPollOptionRepository.Object,
            postPollVoteRepository.Object,
            postQuestionRepository.Object,
            postAnswerRepository.Object,
            tagService.Object,
            coinRewardService.Object,
            notificationService.Object,
            dedupService.Object,
            experienceService.Object,
            postEditHistoryRepository.Object,
            Mock.Of<IAttachmentService>(),
            Options.Create(new ForumEditHistoryOptions()),
            CreateDefaultSystemSettingProvider());

        var posts = new List<PostVo>
        {
            new()
            {
                VoId = 1001,
                VoCategoryId = 101
            },
            new()
            {
                VoId = 1002,
                VoCategoryId = 0
            }
        };

        await service.FillPostListMetadataAsync(posts);

        Assert.Equal("综合讨论", posts[0].VoCategoryName);
        Assert.Equal("投票, 社区", posts[0].VoTags);
        Assert.Equal(["poll", "community"], posts[0].VoTagSlugs);
        Assert.True(posts[0].VoHasPoll);
        Assert.Equal(9, posts[0].VoPollTotalVoteCount);
        Assert.False(posts[0].VoPollIsClosed);
        Assert.True(posts[0].VoIsQuestion);
        Assert.True(posts[0].VoIsSolved);
        Assert.Equal(2, posts[0].VoAnswerCount);

        Assert.False(posts[1].VoHasPoll);
        Assert.Equal(0, posts[1].VoPollTotalVoteCount);
        Assert.False(posts[1].VoPollIsClosed);
        Assert.False(posts[1].VoIsQuestion);
        Assert.False(posts[1].VoIsSolved);
        Assert.Equal(0, posts[1].VoAnswerCount);

        postTagRepository.VerifyAll();
        categoryRepository.VerifyAll();
        tagRepository.VerifyAll();
        postPollRepository.VerifyAll();
        postQuestionRepository.VerifyAll();
    }

    [Fact]
    public async Task GetPostDetailAsync_Should_Return_MinimalQuestionSummary_When_PostIsQuestion()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var userPostLikeRepository = new Mock<IBaseRepository<UserPostLike>>(MockBehavior.Strict);
        var postTagRepository = new Mock<IBaseRepository<PostTag>>(MockBehavior.Strict);
        var categoryRepository = new Mock<IBaseRepository<Category>>(MockBehavior.Strict);
        var tagRepository = new Mock<IBaseRepository<Tag>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postQuestionRepository = new Mock<IBaseRepository<PostQuestion>>(MockBehavior.Strict);
        var postAnswerRepository = new Mock<IBaseRepository<PostAnswer>>(MockBehavior.Strict);
        var tagService = new Mock<ITagService>(MockBehavior.Strict);
        var coinRewardService = new Mock<ICoinRewardService>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var dedupService = new Mock<INotificationDedupService>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var postEditHistoryRepository = new Mock<IBaseRepository<PostEditHistory>>(MockBehavior.Strict);
        var bookmarkRepository = new Mock<IUserPostBookmarkRepository>(MockBehavior.Strict);

        var post = new Post(new PostInitializationOptions("问答详情帖", "这个问题应该怎么分析和解决？")
        {
            AuthorId = 9527,
            AuthorName = "Tester",
            TenantId = 9,
            IsPublished = true
        })
        {
            Id = 1004
        };

        var question = new PostQuestion
        {
            Id = 3002,
            PostId = 1004,
            IsSolved = false,
            AnswerCount = 3,
            AcceptedAnswerId = null,
            IsDeleted = false
        };

        postRepository
            .Setup(repository => repository.QueryByIdAsync(1004))
            .ReturnsAsync(post);
        postRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Post, Post>>>(),
                It.IsAny<Expression<Func<Post, bool>>>()))
            .ReturnsAsync(1);
        postTagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>?>()))
            .ReturnsAsync(new List<PostTag>());
        postPollRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync((PostPoll?)null);
        postQuestionRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostQuestion, bool>>?>()))
            .ReturnsAsync(question);
        postAnswerRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostAnswer, bool>>?>()))
            .ReturnsAsync(new List<PostAnswer>());
        bookmarkRepository
            .Setup(repository => repository.QueryActiveAsync(9, 2001, 1004))
            .ReturnsAsync(new UserPostBookmark
            {
                Id = 8001,
                TenantId = 9,
                UserId = 2001,
                PostId = 1004
            });

        var service = new PostService(
            postRepository.Object,
            userPostLikeRepository.Object,
            postTagRepository.Object,
            categoryRepository.Object,
            tagRepository.Object,
            postPollRepository.Object,
            postPollOptionRepository.Object,
            postPollVoteRepository.Object,
            postQuestionRepository.Object,
            postAnswerRepository.Object,
            tagService.Object,
            coinRewardService.Object,
            notificationService.Object,
            dedupService.Object,
            experienceService.Object,
            postEditHistoryRepository.Object,
            Mock.Of<IAttachmentService>(),
            Options.Create(new ForumEditHistoryOptions()),
            CreateDefaultSystemSettingProvider(),
            userPostBookmarkRepository: bookmarkRepository.Object);

        var result = await service.GetPostDetailAsync(1004, viewerUserId: 2001);

        Assert.NotNull(result);
        Assert.True(result!.VoIsQuestion);
        Assert.False(result.VoIsSolved);
        Assert.Equal(3, result.VoAnswerCount);
        Assert.NotNull(result.VoQuestion);
        Assert.Equal(1004, result.VoQuestion!.VoPostId);
        Assert.Equal(3, result.VoQuestion.VoAnswerCount);
        Assert.Empty(result.VoQuestion.VoAnswers);
        Assert.Null(result.VoPoll);
        Assert.True(result.VoIsBookmarked);

        postRepository.VerifyAll();
        postTagRepository.VerifyAll();
        postPollRepository.VerifyAll();
        postQuestionRepository.VerifyAll();
        postAnswerRepository.VerifyAll();
        bookmarkRepository.VerifyAll();
    }

    [Fact]
    public async Task GetPostDetailAsync_Should_Backfill_PublicId_When_LegacyPostHasNoPublicId()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var userPostLikeRepository = new Mock<IBaseRepository<UserPostLike>>(MockBehavior.Strict);
        var postTagRepository = new Mock<IBaseRepository<PostTag>>(MockBehavior.Strict);
        var categoryRepository = new Mock<IBaseRepository<Category>>(MockBehavior.Strict);
        var tagRepository = new Mock<IBaseRepository<Tag>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postQuestionRepository = new Mock<IBaseRepository<PostQuestion>>(MockBehavior.Strict);
        var postAnswerRepository = new Mock<IBaseRepository<PostAnswer>>(MockBehavior.Strict);
        var tagService = new Mock<ITagService>(MockBehavior.Strict);
        var coinRewardService = new Mock<ICoinRewardService>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var dedupService = new Mock<INotificationDedupService>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var postEditHistoryRepository = new Mock<IBaseRepository<PostEditHistory>>(MockBehavior.Strict);

        var post = new Post(new PostInitializationOptions("旧帖子", "缺少公开 ID")
        {
            AuthorId = 9527,
            AuthorName = "Tester",
            TenantId = 9,
            IsPublished = true
        })
        {
            Id = 1006,
            PublicId = null
        };

        postRepository
            .Setup(repository => repository.QueryByIdAsync(1006))
            .ReturnsAsync(post);
        postRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Post, Post>>>(),
                It.IsAny<Expression<Func<Post, bool>>>()))
            .ReturnsAsync(1);
        postTagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>?>()))
            .ReturnsAsync(new List<PostTag>());
        postPollRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync((PostPoll?)null);
        postQuestionRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostQuestion, bool>>?>()))
            .ReturnsAsync((PostQuestion?)null);

        var service = new PostService(
            postRepository.Object,
            userPostLikeRepository.Object,
            postTagRepository.Object,
            categoryRepository.Object,
            tagRepository.Object,
            postPollRepository.Object,
            postPollOptionRepository.Object,
            postPollVoteRepository.Object,
            postQuestionRepository.Object,
            postAnswerRepository.Object,
            tagService.Object,
            coinRewardService.Object,
            notificationService.Object,
            dedupService.Object,
            experienceService.Object,
            postEditHistoryRepository.Object,
            Mock.Of<IAttachmentService>(),
            Options.Create(new ForumEditHistoryOptions()),
            CreateDefaultSystemSettingProvider());

        var result = await service.GetPostDetailAsync(1006, viewerUserId: null);

        Assert.NotNull(result);
        Assert.StartsWith("pst_", result!.VoPublicId);
        Assert.Equal(post.PublicId, result.VoPublicId);
        postRepository.Verify(repository => repository.UpdateColumnsAsync(
            It.IsAny<Expression<Func<Post, Post>>>(),
            It.IsAny<Expression<Func<Post, bool>>>()), Times.Once);

        postRepository.VerifyAll();
        postTagRepository.VerifyAll();
        postPollRepository.VerifyAll();
        postQuestionRepository.VerifyAll();
    }

    [Fact]
    public async Task GetPostDetailAsync_Should_Return_QuestionAnswers_When_AnswersExist()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var userPostLikeRepository = new Mock<IBaseRepository<UserPostLike>>(MockBehavior.Strict);
        var postTagRepository = new Mock<IBaseRepository<PostTag>>(MockBehavior.Strict);
        var categoryRepository = new Mock<IBaseRepository<Category>>(MockBehavior.Strict);
        var tagRepository = new Mock<IBaseRepository<Tag>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postQuestionRepository = new Mock<IBaseRepository<PostQuestion>>(MockBehavior.Strict);
        var postAnswerRepository = new Mock<IBaseRepository<PostAnswer>>(MockBehavior.Strict);
        var tagService = new Mock<ITagService>(MockBehavior.Strict);
        var coinRewardService = new Mock<ICoinRewardService>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var dedupService = new Mock<INotificationDedupService>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var postEditHistoryRepository = new Mock<IBaseRepository<PostEditHistory>>(MockBehavior.Strict);

        var post = new Post(new PostInitializationOptions("问答详情帖", "看看回答列表")
        {
            AuthorId = 9527,
            AuthorName = "Tester",
            TenantId = 9,
            IsPublished = true
        })
        {
            Id = 1005
        };

        var question = new PostQuestion
        {
            Id = 3003,
            PostId = 1005,
            IsSolved = false,
            AnswerCount = 2,
            IsDeleted = false
        };

        var answers = new List<PostAnswer>
        {
            new()
            {
                Id = 4002,
                PostId = 1005,
                AuthorId = 2002,
                AuthorName = "Alice",
                Content = "先检查配置。",
                IsAccepted = true,
                CreateTime = DateTime.UtcNow.AddMinutes(-2)
            },
            new()
            {
                Id = 4001,
                PostId = 1005,
                AuthorId = 2001,
                AuthorName = "Bob",
                Content = "可以先看日志。",
                IsAccepted = false,
                CreateTime = DateTime.UtcNow.AddMinutes(-5)
            }
        };

        postRepository
            .Setup(repository => repository.QueryByIdAsync(1005))
            .ReturnsAsync(post);
        postRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Post, Post>>>(),
                It.IsAny<Expression<Func<Post, bool>>>()))
            .ReturnsAsync(1);
        postTagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>?>()))
            .ReturnsAsync(new List<PostTag>());
        postPollRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync((PostPoll?)null);
        postQuestionRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostQuestion, bool>>?>()))
            .ReturnsAsync(question);
        postAnswerRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostAnswer, bool>>?>()))
            .ReturnsAsync(answers);

        var service = new PostService(
            postRepository.Object,
            userPostLikeRepository.Object,
            postTagRepository.Object,
            categoryRepository.Object,
            tagRepository.Object,
            postPollRepository.Object,
            postPollOptionRepository.Object,
            postPollVoteRepository.Object,
            postQuestionRepository.Object,
            postAnswerRepository.Object,
            tagService.Object,
            coinRewardService.Object,
            notificationService.Object,
            dedupService.Object,
            experienceService.Object,
            postEditHistoryRepository.Object,
            Mock.Of<IAttachmentService>(),
            Options.Create(new ForumEditHistoryOptions()),
            CreateDefaultSystemSettingProvider());

        var result = await service.GetPostDetailAsync(1005, viewerUserId: null);

        Assert.NotNull(result);
        Assert.NotNull(result!.VoQuestion);
        Assert.Equal(2, result.VoQuestion!.VoAnswers.Count);
        Assert.Equal(4002, result.VoQuestion.VoAnswers[0].VoAnswerId);
        Assert.Equal(4001, result.VoQuestion.VoAnswers[1].VoAnswerId);

        postRepository.VerifyAll();
        postTagRepository.VerifyAll();
        postPollRepository.VerifyAll();
        postQuestionRepository.VerifyAll();
        postAnswerRepository.VerifyAll();
    }

    [Fact]
    public async Task AddAnswerAsync_Should_CreateAnswer_And_UpdateQuestionCount()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var userPostLikeRepository = new Mock<IBaseRepository<UserPostLike>>(MockBehavior.Strict);
        var postTagRepository = new Mock<IBaseRepository<PostTag>>(MockBehavior.Strict);
        var categoryRepository = new Mock<IBaseRepository<Category>>(MockBehavior.Strict);
        var tagRepository = new Mock<IBaseRepository<Tag>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postQuestionRepository = new Mock<IBaseRepository<PostQuestion>>(MockBehavior.Strict);
        var postAnswerRepository = new Mock<IBaseRepository<PostAnswer>>(MockBehavior.Strict);
        var tagService = new Mock<ITagService>(MockBehavior.Strict);
        var coinRewardService = new Mock<ICoinRewardService>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var dedupService = new Mock<INotificationDedupService>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var postEditHistoryRepository = new Mock<IBaseRepository<PostEditHistory>>(MockBehavior.Strict);

        var post = new Post(new PostInitializationOptions("问答帖", "怎么处理？")
        {
            AuthorId = 9527,
            AuthorName = "Tester",
            TenantId = 9,
            IsPublished = true
        })
        {
            Id = 1006
        };

        var question = new PostQuestion
        {
            Id = 3004,
            PostId = 1006,
            IsSolved = false,
            AnswerCount = 0,
            IsDeleted = false
        };

        postRepository
            .Setup(repository => repository.QueryByIdAsync(1006))
            .ReturnsAsync(post);
        postQuestionRepository
            .SetupSequence(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostQuestion, bool>>?>()))
            .ReturnsAsync(question)
            .ReturnsAsync(question);
        postQuestionRepository
            .Setup(repository => repository.UpdateAsync(It.Is<PostQuestion>(item =>
                item.PostId == 1006 &&
                item.AnswerCount == 1 &&
                item.ModifyBy == "Alice" &&
                item.ModifyId == 2001)))
            .ReturnsAsync(true);
        postAnswerRepository
            .Setup(repository => repository.AddAsync(It.Is<PostAnswer>(answer =>
                answer.PostId == 1006 &&
                answer.AuthorId == 2001 &&
                answer.AuthorName == "Alice" &&
                answer.Content == "先检查数据库连接。" &&
                !answer.IsAccepted &&
                answer.TenantId == 9 &&
                answer.CreateBy == "Alice" &&
                answer.CreateId == 2001)))
            .ReturnsAsync(4003);
        postAnswerRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostAnswer, bool>>?>()))
            .ReturnsAsync(
            [
                new PostAnswer
                {
                    Id = 4003,
                    PostId = 1006,
                    AuthorId = 2001,
                    AuthorName = "Alice",
                    Content = "先检查数据库连接。",
                    IsAccepted = false,
                    CreateTime = DateTime.UtcNow
                }
            ]);

        var service = new PostService(
            postRepository.Object,
            userPostLikeRepository.Object,
            postTagRepository.Object,
            categoryRepository.Object,
            tagRepository.Object,
            postPollRepository.Object,
            postPollOptionRepository.Object,
            postPollVoteRepository.Object,
            postQuestionRepository.Object,
            postAnswerRepository.Object,
            tagService.Object,
            coinRewardService.Object,
            notificationService.Object,
            dedupService.Object,
            experienceService.Object,
            postEditHistoryRepository.Object,
            Mock.Of<IAttachmentService>(),
            Options.Create(new ForumEditHistoryOptions()),
            CreateDefaultSystemSettingProvider());

        var result = await service.AddAnswerAsync(1006, "  先检查数据库连接。  ", 2001, "Alice", 9);

        Assert.Equal(1006, result.VoPostId);
        Assert.Equal(1, result.VoAnswerCount);
        Assert.Single(result.VoAnswers);
        Assert.Equal("先检查数据库连接。", result.VoAnswers[0].VoContent);

        postRepository.VerifyAll();
        postQuestionRepository.VerifyAll();
        postAnswerRepository.VerifyAll();
    }

    [Fact]
    public async Task AcceptAnswerAsync_Should_MarkAcceptedAnswer_And_SolveQuestion()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var userPostLikeRepository = new Mock<IBaseRepository<UserPostLike>>(MockBehavior.Strict);
        var postTagRepository = new Mock<IBaseRepository<PostTag>>(MockBehavior.Strict);
        var categoryRepository = new Mock<IBaseRepository<Category>>(MockBehavior.Strict);
        var tagRepository = new Mock<IBaseRepository<Tag>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postQuestionRepository = new Mock<IBaseRepository<PostQuestion>>(MockBehavior.Strict);
        var postAnswerRepository = new Mock<IBaseRepository<PostAnswer>>(MockBehavior.Strict);
        var tagService = new Mock<ITagService>(MockBehavior.Strict);
        var coinRewardService = new Mock<ICoinRewardService>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var dedupService = new Mock<INotificationDedupService>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var postEditHistoryRepository = new Mock<IBaseRepository<PostEditHistory>>(MockBehavior.Strict);

        var post = new Post(new PostInitializationOptions("问答帖", "谁来回答")
        {
            AuthorId = 9527,
            AuthorName = "Owner",
            TenantId = 9,
            IsPublished = true
        })
        {
            Id = 1007
        };

        var question = new PostQuestion
        {
            Id = 3005,
            PostId = 1007,
            IsSolved = false,
            AcceptedAnswerId = null,
            AnswerCount = 2,
            IsDeleted = false
        };

        var answer = new PostAnswer
        {
            Id = 4004,
            PostId = 1007,
            AuthorId = 2002,
            AuthorName = "Alice",
            Content = "这是正确答案",
            IsAccepted = false,
            IsDeleted = false,
            CreateTime = DateTime.UtcNow
        };

        postRepository
            .Setup(repository => repository.QueryByIdAsync(1007))
            .ReturnsAsync(post);
        postQuestionRepository
            .SetupSequence(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostQuestion, bool>>?>()))
            .ReturnsAsync(question)
            .ReturnsAsync(question);
        postAnswerRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostAnswer, bool>>?>()))
            .ReturnsAsync(answer);
        postAnswerRepository
            .Setup(repository => repository.UpdateAsync(It.Is<PostAnswer>(item =>
                item.Id == 4004 &&
                item.IsAccepted &&
                item.ModifyBy == "Owner" &&
                item.ModifyId == 9527)))
            .ReturnsAsync(true);
        postQuestionRepository
            .Setup(repository => repository.UpdateAsync(It.Is<PostQuestion>(item =>
                item.PostId == 1007 &&
                item.IsSolved &&
                item.AcceptedAnswerId == 4004 &&
                item.ModifyBy == "Owner" &&
                item.ModifyId == 9527)))
            .ReturnsAsync(true);
        postAnswerRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostAnswer, bool>>?>()))
            .ReturnsAsync([answer]);

        var service = new PostService(
            postRepository.Object,
            userPostLikeRepository.Object,
            postTagRepository.Object,
            categoryRepository.Object,
            tagRepository.Object,
            postPollRepository.Object,
            postPollOptionRepository.Object,
            postPollVoteRepository.Object,
            postQuestionRepository.Object,
            postAnswerRepository.Object,
            tagService.Object,
            coinRewardService.Object,
            notificationService.Object,
            dedupService.Object,
            experienceService.Object,
            postEditHistoryRepository.Object,
            Mock.Of<IAttachmentService>(),
            Options.Create(new ForumEditHistoryOptions()),
            CreateDefaultSystemSettingProvider());

        var result = await service.AcceptAnswerAsync(1007, 4004, 9527, "Owner");

        Assert.True(result.VoIsSolved);
        Assert.Equal(4004, result.VoAcceptedAnswerId);
        Assert.Single(result.VoAnswers);
        Assert.True(result.VoAnswers[0].VoIsAccepted);

        postRepository.VerifyAll();
        postQuestionRepository.VerifyAll();
        postAnswerRepository.VerifyAll();
    }

    [Fact]
    public async Task AcceptAnswerAsync_Should_Throw_When_QuestionHasAlreadyBeenSolved()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var userPostLikeRepository = new Mock<IBaseRepository<UserPostLike>>(MockBehavior.Strict);
        var postTagRepository = new Mock<IBaseRepository<PostTag>>(MockBehavior.Strict);
        var categoryRepository = new Mock<IBaseRepository<Category>>(MockBehavior.Strict);
        var tagRepository = new Mock<IBaseRepository<Tag>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postQuestionRepository = new Mock<IBaseRepository<PostQuestion>>(MockBehavior.Strict);
        var postAnswerRepository = new Mock<IBaseRepository<PostAnswer>>(MockBehavior.Strict);
        var tagService = new Mock<ITagService>(MockBehavior.Strict);
        var coinRewardService = new Mock<ICoinRewardService>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var dedupService = new Mock<INotificationDedupService>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var postEditHistoryRepository = new Mock<IBaseRepository<PostEditHistory>>(MockBehavior.Strict);

        var post = new Post(new PostInitializationOptions("已解决问答帖", "这个问题已经解决了")
        {
            AuthorId = 9527,
            AuthorName = "Owner",
            TenantId = 9,
            IsPublished = true
        })
        {
            Id = 1008
        };

        var solvedQuestion = new PostQuestion
        {
            Id = 3006,
            PostId = 1008,
            IsSolved = true,
            AcceptedAnswerId = 4004,
            AnswerCount = 2,
            IsDeleted = false
        };

        postRepository
            .Setup(repository => repository.QueryByIdAsync(1008))
            .ReturnsAsync(post);
        postQuestionRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostQuestion, bool>>?>()))
            .ReturnsAsync(solvedQuestion);

        var service = new PostService(
            postRepository.Object,
            userPostLikeRepository.Object,
            postTagRepository.Object,
            categoryRepository.Object,
            tagRepository.Object,
            postPollRepository.Object,
            postPollOptionRepository.Object,
            postPollVoteRepository.Object,
            postQuestionRepository.Object,
            postAnswerRepository.Object,
            tagService.Object,
            coinRewardService.Object,
            notificationService.Object,
            dedupService.Object,
            experienceService.Object,
            postEditHistoryRepository.Object,
            Mock.Of<IAttachmentService>(),
            Options.Create(new ForumEditHistoryOptions()),
            CreateDefaultSystemSettingProvider());

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.AcceptAnswerAsync(1008, 4005, 9527, "Owner"));

        Assert.Equal("当前问题已采纳答案", exception.Message);

        postRepository.VerifyAll();
        postQuestionRepository.VerifyAll();
        postAnswerRepository.Verify(
            repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostAnswer, bool>>?>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdatePostAsync_Should_Skip_EditHistory_When_Content_Category_And_Tags_Unchanged()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var userPostLikeRepository = new Mock<IBaseRepository<UserPostLike>>(MockBehavior.Strict);
        var postTagRepository = new Mock<IBaseRepository<PostTag>>(MockBehavior.Strict);
        var categoryRepository = new Mock<IBaseRepository<Category>>(MockBehavior.Strict);
        var tagRepository = new Mock<IBaseRepository<Tag>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postQuestionRepository = new Mock<IBaseRepository<PostQuestion>>(MockBehavior.Strict);
        var postAnswerRepository = new Mock<IBaseRepository<PostAnswer>>(MockBehavior.Strict);
        var tagService = new Mock<ITagService>(MockBehavior.Strict);
        var coinRewardService = new Mock<ICoinRewardService>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var dedupService = new Mock<INotificationDedupService>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var postEditHistoryRepository = new Mock<IBaseRepository<PostEditHistory>>(MockBehavior.Strict);

        var post = new Post(new PostInitializationOptions("原问题标题", "原问题内容")
        {
            AuthorId = 9527,
            AuthorName = "Owner",
            TenantId = 9,
            CategoryId = 101,
            IsPublished = true
        })
        {
            Id = 2001,
            EditCount = 0
        };

        var existingTag = new Tag("问答")
        {
            Id = 601,
            IsEnabled = true,
            IsDeleted = false
        };

        postRepository
            .Setup(repository => repository.QueryByIdAsync(2001))
            .ReturnsAsync(post);
        postTagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>?>()))
            .ReturnsAsync(
            [
                new PostTag(2001, 601)
            ]);
        tagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Tag, bool>>?>()))
            .ReturnsAsync((Expression<Func<Tag, bool>>? expression) =>
            {
                var tags = new List<Tag> { existingTag };
                if (expression == null)
                {
                    return tags;
                }

                var predicate = expression.Compile();
                return tags.Where(predicate).ToList();
            });

        var service = new PostService(
            postRepository.Object,
            userPostLikeRepository.Object,
            postTagRepository.Object,
            categoryRepository.Object,
            tagRepository.Object,
            postPollRepository.Object,
            postPollOptionRepository.Object,
            postPollVoteRepository.Object,
            postQuestionRepository.Object,
            postAnswerRepository.Object,
            tagService.Object,
            coinRewardService.Object,
            notificationService.Object,
            dedupService.Object,
            experienceService.Object,
            postEditHistoryRepository.Object,
            Mock.Of<IAttachmentService>(),
            Options.Create(new ForumEditHistoryOptions
            {
                Enable = true,
                Post = new ForumPostEditHistoryOptions
                {
                    EnableHistory = true,
                    HistorySaveEditCount = 10,
                    MaxEditCount = 20,
                    MaxHistoryRecords = 20
                }
            }),
            CreateDefaultSystemSettingProvider());

        await service.UpdatePostAsync(
            2001,
            " 原问题标题 ",
            "原问题内容",
            null,
            ["问答"],
            allowCreateTag: false,
            operatorId: 9527,
            operatorName: "Owner");

        postRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Post>()), Times.Never);
        postEditHistoryRepository.Verify(
            repository => repository.QueryCountAsync(It.IsAny<Expression<Func<PostEditHistory, bool>>?>()),
            Times.Never);
        postEditHistoryRepository.Verify(repository => repository.AddAsync(It.IsAny<PostEditHistory>()), Times.Never);
        postRepository.VerifyAll();
        postTagRepository.VerifyAll();
        tagRepository.VerifyAll();
    }

    [Fact]
    public async Task UpdatePostAsync_Should_UseContentRevisionCas_When_QuestionPostIsUpdated()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var userPostLikeRepository = new Mock<IBaseRepository<UserPostLike>>(MockBehavior.Strict);
        var postTagRepository = new Mock<IBaseRepository<PostTag>>(MockBehavior.Strict);
        var categoryRepository = new Mock<IBaseRepository<Category>>(MockBehavior.Strict);
        var tagRepository = new Mock<IBaseRepository<Tag>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postQuestionRepository = new Mock<IBaseRepository<PostQuestion>>(MockBehavior.Strict);
        var postAnswerRepository = new Mock<IBaseRepository<PostAnswer>>(MockBehavior.Strict);
        var tagService = new Mock<ITagService>(MockBehavior.Strict);
        var coinRewardService = new Mock<ICoinRewardService>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var dedupService = new Mock<INotificationDedupService>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var postEditHistoryRepository = new Mock<IBaseRepository<PostEditHistory>>(MockBehavior.Strict);

        var post = new Post(new PostInitializationOptions("原问题标题", "原问题内容")
        {
            AuthorId = 9527,
            AuthorName = "Owner",
            TenantId = 9,
            CategoryId = 101,
            IsPublished = true
        })
        {
            Id = 2001,
            EditCount = 0
        };

        var existingTag = new Tag("问答")
        {
            Id = 601,
            IsEnabled = true,
            IsDeleted = false
        };

        postRepository
            .Setup(repository => repository.QueryByIdAsync(2001))
            .ReturnsAsync(post);
        postRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<Post, Post>>>(),
                It.IsAny<Expression<Func<Post, bool>>>()))
            .ReturnsAsync(1);
        categoryRepository
            .Setup(repository => repository.QueryByIdAsync(101))
            .ReturnsAsync(new Category("问答")
            {
                Id = 101,
                IsEnabled = true,
                IsDeleted = false
            });

        postTagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>?>()))
            .ReturnsAsync(
            [
                new PostTag(2001, 601)
            ]);

        tagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Tag, bool>>?>()))
            .ReturnsAsync((Expression<Func<Tag, bool>>? expression) =>
            {
                var tags = new List<Tag> { existingTag };
                if (expression == null)
                {
                    return tags;
                }

                var predicate = expression.Compile();
                return tags.Where(predicate).ToList();
            });

        var service = new PostService(
            postRepository.Object,
            userPostLikeRepository.Object,
            postTagRepository.Object,
            categoryRepository.Object,
            tagRepository.Object,
            postPollRepository.Object,
            postPollOptionRepository.Object,
            postPollVoteRepository.Object,
            postQuestionRepository.Object,
            postAnswerRepository.Object,
            tagService.Object,
            coinRewardService.Object,
            notificationService.Object,
            dedupService.Object,
            experienceService.Object,
            postEditHistoryRepository.Object,
            Mock.Of<IAttachmentService>(),
            Options.Create(new ForumEditHistoryOptions
            {
                Enable = true,
                Post = new ForumPostEditHistoryOptions
                {
                    EnableHistory = true,
                    HistorySaveEditCount = 10,
                    MaxEditCount = 20,
                    MaxHistoryRecords = 20
                }
            }),
            CreateDefaultSystemSettingProvider());

        await service.UpdatePostAsync(
            2001,
            "更新后的问题标题",
            "更新后的问题内容已经补充完整",
            null,
            ["问答"],
            allowCreateTag: false,
            operatorId: 9527,
            operatorName: "Owner");

        postRepository.VerifyAll();
        postTagRepository.VerifyAll();
        tagRepository.VerifyAll();
        postEditHistoryRepository.VerifyAll();
        postEditHistoryRepository.Verify(
            repository => repository.AddAsync(It.IsAny<PostEditHistory>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPostEditHistoryPageAsync_Should_Normalize_Paging_And_Map_Result()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var userPostLikeRepository = new Mock<IBaseRepository<UserPostLike>>(MockBehavior.Strict);
        var postTagRepository = new Mock<IBaseRepository<PostTag>>(MockBehavior.Strict);
        var categoryRepository = new Mock<IBaseRepository<Category>>(MockBehavior.Strict);
        var tagRepository = new Mock<IBaseRepository<Tag>>(MockBehavior.Strict);
        var postPollRepository = new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict);
        var postPollOptionRepository = new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict);
        var postPollVoteRepository = new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict);
        var postQuestionRepository = new Mock<IBaseRepository<PostQuestion>>(MockBehavior.Strict);
        var postAnswerRepository = new Mock<IBaseRepository<PostAnswer>>(MockBehavior.Strict);
        var tagService = new Mock<ITagService>(MockBehavior.Strict);
        var coinRewardService = new Mock<ICoinRewardService>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var dedupService = new Mock<INotificationDedupService>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var postEditHistoryRepository = new Mock<IBaseRepository<PostEditHistory>>(MockBehavior.Strict);

        var histories = new List<PostEditHistory>
        {
            new()
            {
                Id = 8001,
                PostId = 2001,
                EditSequence = 2,
                OldTitle = "旧标题",
                NewTitle = "新标题",
                OldContent = "旧内容",
                NewContent = "新内容",
                EditorId = 9527,
                EditorName = "Owner"
            }
        };

        var historyVos = new List<PostEditHistoryVo>
        {
            new()
            {
                VoId = 8001,
                VoPostId = 2001,
                VoEditSequence = 2,
                VoOldTitle = "旧标题",
                VoNewTitle = "新标题",
                VoOldContent = "旧内容",
                VoNewContent = "新内容",
                VoEditorId = 9527,
                VoEditorName = "Owner"
            }
        };

        postEditHistoryRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<PostEditHistory, bool>>?>(),
                1,
                100,
                It.IsAny<Expression<Func<PostEditHistory, object>>>(),
                OrderByType.Desc,
                It.IsAny<Expression<Func<PostEditHistory, object>>>(),
                OrderByType.Desc))
            .ReturnsAsync((histories, 1));

        var service = new PostService(
            postRepository.Object,
            userPostLikeRepository.Object,
            postTagRepository.Object,
            categoryRepository.Object,
            tagRepository.Object,
            postPollRepository.Object,
            postPollOptionRepository.Object,
            postPollVoteRepository.Object,
            postQuestionRepository.Object,
            postAnswerRepository.Object,
            tagService.Object,
            coinRewardService.Object,
            notificationService.Object,
            dedupService.Object,
            experienceService.Object,
            postEditHistoryRepository.Object,
            Mock.Of<IAttachmentService>(),
            Options.Create(new ForumEditHistoryOptions()),
            CreateDefaultSystemSettingProvider());

        var (result, total) = await service.GetPostEditHistoryPageAsync(2001, 0, 200);

        Assert.Single(result);
        Assert.Equal(1, total);
        Assert.Equal(8001, result[0].VoId);
        Assert.Equal("新标题", result[0].VoNewTitle);

        postEditHistoryRepository.Verify(repository => repository.QueryPageAsync(
            It.IsAny<Expression<Func<PostEditHistory, bool>>?>(),
            1,
            100,
            It.IsAny<Expression<Func<PostEditHistory, object>>>(),
            OrderByType.Desc,
            It.IsAny<Expression<Func<PostEditHistory, object>>>(),
            OrderByType.Desc), Times.Once);
    }

    [Fact]
    public async Task GetPostDetailAsync_Should_Not_Expose_Disabled_Post()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        postRepository
            .Setup(repository => repository.QueryByIdAsync(9527))
            .ReturnsAsync(new Post(new PostInitializationOptions("已禁用帖子", "正文")
            {
                IsPublished = true,
                IsEnabled = false
            })
            {
                Id = 9527,
                IsDeleted = false
            });
        var service = new PostService(
            postRepository.Object,
            Mock.Of<IBaseRepository<UserPostLike>>(),
            Mock.Of<IBaseRepository<PostTag>>(),
            Mock.Of<IBaseRepository<Category>>(),
            Mock.Of<IBaseRepository<Tag>>(),
            Mock.Of<IBaseRepository<PostPoll>>(),
            Mock.Of<IBaseRepository<PostPollOption>>(),
            Mock.Of<IBaseRepository<PostPollVote>>(),
            Mock.Of<IBaseRepository<PostQuestion>>(),
            Mock.Of<IBaseRepository<PostAnswer>>(),
            Mock.Of<ITagService>(),
            Mock.Of<ICoinRewardService>(),
            Mock.Of<INotificationService>(),
            Mock.Of<INotificationDedupService>(),
            Mock.Of<IExperienceService>(),
            Mock.Of<IBaseRepository<PostEditHistory>>(),
            Mock.Of<IAttachmentService>(),
            Options.Create(new ForumEditHistoryOptions()),
            CreateDefaultSystemSettingProvider());

        var result = await service.GetPostDetailAsync(9527);

        Assert.Null(result);
        postRepository.VerifyAll();
    }

    private static ISystemSettingProvider CreateDefaultSystemSettingProvider(
        int? postTitleMaxLength = null,
        int? postBodyMaxLength = null,
        int? postSummaryMaxLength = null)
    {
        var provider = new Mock<ISystemSettingProvider>();
        provider
            .Setup(item => item.GetInt32Async(SystemConfigDefaults.PostTitleMinLengthKey))
            .ReturnsAsync(int.Parse(SystemConfigDefaults.DefaultPostTitleMinLength));
        provider
            .Setup(item => item.GetInt32Async(SystemConfigDefaults.PostTitleMaxLengthKey))
            .ReturnsAsync(postTitleMaxLength ?? int.Parse(SystemConfigDefaults.DefaultPostTitleMaxLength));
        provider
            .Setup(item => item.GetInt32Async(SystemConfigDefaults.PostBodyMinLengthKey))
            .ReturnsAsync(int.Parse(SystemConfigDefaults.DefaultPostBodyMinLength));
        provider
            .Setup(item => item.GetInt32Async(SystemConfigDefaults.PostBodyMaxLengthKey))
            .ReturnsAsync(postBodyMaxLength ?? int.Parse(SystemConfigDefaults.DefaultPostBodyMaxLength));
        provider
            .Setup(item => item.GetInt32Async(SystemConfigDefaults.PostSummaryMaxLengthKey))
            .ReturnsAsync(postSummaryMaxLength ?? int.Parse(SystemConfigDefaults.DefaultPostSummaryMaxLength));

        return provider.Object;
    }
}
