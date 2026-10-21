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
    public async Task GetPollPostPageAsync_Should_Return_Empty_When_NoPollPostsExist()
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

        postPollRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync(new List<PostPoll>());

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

        var (data, totalCount) = await service.GetPollPostPageAsync(sortBy: "newest");

        Assert.Empty(data);
        Assert.Equal(0, totalCount);

        postPollRepository.VerifyAll();
        postRepository.Verify(repository => repository.QueryPageAsync(
            It.IsAny<Expression<Func<Post, bool>>>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<Expression<Func<Post, object>>>(),
            It.IsAny<OrderByType>()), Times.Never);
    }

    [Fact]
    public async Task GetPollPostPageAsync_Should_Filter_Closed_Status()
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

        var polls = new List<PostPoll>
        {
            new()
            {
                Id = 2001,
                PostId = 1001,
                IsClosed = false,
                EndTime = DateTime.UtcNow.AddHours(2),
                IsDeleted = false
            },
            new()
            {
                Id = 2002,
                PostId = 1002,
                IsClosed = true,
                EndTime = DateTime.UtcNow.AddHours(2),
                IsDeleted = false
            },
            new()
            {
                Id = 2003,
                PostId = 1003,
                IsClosed = false,
                EndTime = DateTime.UtcNow.AddHours(-1),
                IsDeleted = false
            },
            new()
            {
                Id = 2004,
                PostId = 1004,
                IsClosed = false,
                EndTime = DateTime.UtcNow.AddHours(2),
                IsDeleted = false
            }
        };

        var posts = new List<Post>
        {
            new(new PostInitializationOptions("进行中投票", "正文"))
            {
                Id = 1001,
                IsPublished = true,
                IsDeleted = false,
                CreateTime = DateTime.UtcNow.AddMinutes(-10)
            },
            new(new PostInitializationOptions("手动关闭投票", "正文"))
            {
                Id = 1002,
                IsPublished = true,
                IsDeleted = false,
                CreateTime = DateTime.UtcNow.AddMinutes(-20)
            },
            new(new PostInitializationOptions("已到截止时间", "正文"))
            {
                Id = 1003,
                IsPublished = true,
                IsDeleted = false,
                CreateTime = DateTime.UtcNow.AddMinutes(-30)
            },
            new(new PostInitializationOptions("已禁用投票", "正文"))
            {
                Id = 1004,
                IsPublished = true,
                IsEnabled = false,
                IsDeleted = false,
                CreateTime = DateTime.UtcNow.AddMinutes(-40)
            }
        };

        postPollRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync((Expression<Func<PostPoll, bool>>? expression) =>
            {
                var predicate = expression?.Compile() ?? (_ => true);
                return polls.Where(predicate).ToList();
            });
        postRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<Post, bool>>>(),
                1,
                20,
                It.IsAny<Expression<Func<Post, object>>>(),
                OrderByType.Desc))
            .ReturnsAsync((Expression<Func<Post, bool>>? expression,
                int pageIndex,
                int pageSize,
                Expression<Func<Post, object>>? orderByExpression,
                OrderByType orderByType) =>
            {
                var predicate = expression?.Compile() ?? (_ => true);
                var filtered = posts.Where(predicate).ToList();
                return (filtered, filtered.Count);
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
            CreateDefaultSystemSettingProvider());

        var (closedPosts, closedTotalCount) = await service.GetPollPostPageAsync(sortBy: "newest", isClosed: true);
        var (activePosts, activeTotalCount) = await service.GetPollPostPageAsync(sortBy: "newest", isClosed: false);

        Assert.Equal(2, closedTotalCount);
        Assert.Equal([1002L, 1003L], closedPosts.Select(post => post.VoId).OrderBy(id => id).ToArray());

        Assert.Equal(1, activeTotalCount);
        Assert.Single(activePosts);
        Assert.Equal(1001, activePosts[0].VoId);

        postPollRepository.Verify(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostPoll, bool>>?>()), Times.Exactly(2));
        postRepository.Verify(repository => repository.QueryPageAsync(
            It.IsAny<Expression<Func<Post, bool>>>(),
            1,
            20,
            It.IsAny<Expression<Func<Post, object>>>(),
            OrderByType.Desc), Times.Exactly(2));
    }

    [Fact]
    public async Task GetPollPostPageAsync_Should_Order_By_Vote_Count_When_SortByVotes()
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

        var now = DateTime.UtcNow;
        var polls = new List<PostPoll>
        {
            new()
            {
                Id = 3001,
                PostId = 1101,
                TotalVoteCount = 12,
                IsClosed = false,
                EndTime = now.AddHours(3),
                IsDeleted = false
            },
            new()
            {
                Id = 3002,
                PostId = 1102,
                TotalVoteCount = 42,
                IsClosed = false,
                EndTime = now.AddHours(3),
                IsDeleted = false
            },
            new()
            {
                Id = 3003,
                PostId = 1103,
                TotalVoteCount = 42,
                IsClosed = false,
                EndTime = now.AddHours(3),
                IsDeleted = false
            }
        };

        var posts = new List<Post>
        {
            new(new PostInitializationOptions("票数较少", "正文"))
            {
                Id = 1101,
                IsPublished = true,
                IsDeleted = false,
                IsTop = false,
                CreateTime = now.AddMinutes(-10)
            },
            new(new PostInitializationOptions("票数较多但较旧", "正文"))
            {
                Id = 1102,
                IsPublished = true,
                IsDeleted = false,
                IsTop = false,
                CreateTime = now.AddMinutes(-30)
            },
            new(new PostInitializationOptions("票数较多且较新", "正文"))
            {
                Id = 1103,
                IsPublished = true,
                IsDeleted = false,
                IsTop = false,
                CreateTime = now.AddMinutes(-5)
            }
        };

        postPollRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync((Expression<Func<PostPoll, bool>>? expression) =>
            {
                var predicate = expression?.Compile() ?? (_ => true);
                return polls.Where(predicate).ToList();
            });
        postRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Post, bool>>?>()))
            .ReturnsAsync((Expression<Func<Post, bool>>? expression) =>
            {
                var predicate = expression?.Compile() ?? (_ => true);
                return posts.Where(predicate).ToList();
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
            CreateDefaultSystemSettingProvider());

        var (data, totalCount) = await service.GetPollPostPageAsync(sortBy: "votes");

        Assert.Equal(3, totalCount);
        Assert.Equal([1103L, 1102L, 1101L], data.Select(post => post.VoId).ToArray());

        postPollRepository.Verify(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostPoll, bool>>?>()), Times.Once);
        postRepository.Verify(repository => repository.QueryAsync(It.IsAny<Expression<Func<Post, bool>>?>()), Times.Once);
        postRepository.Verify(repository => repository.QueryPageAsync(
            It.IsAny<Expression<Func<Post, bool>>>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<Expression<Func<Post, object>>>(),
            It.IsAny<OrderByType>()), Times.Never);
    }

    [Fact]
    public async Task GetPollPostPageAsync_Should_Order_By_Deadline_When_SortByDeadline()
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

        var now = DateTime.UtcNow;
        var polls = new List<PostPoll>
        {
            new()
            {
                Id = 4001,
                PostId = 1201,
                TotalVoteCount = 3,
                IsClosed = false,
                EndTime = now.AddMinutes(30),
                IsDeleted = false
            },
            new()
            {
                Id = 4002,
                PostId = 1202,
                TotalVoteCount = 20,
                IsClosed = false,
                EndTime = now.AddHours(3),
                IsDeleted = false
            },
            new()
            {
                Id = 4003,
                PostId = 1203,
                TotalVoteCount = 8,
                IsClosed = false,
                EndTime = null,
                IsDeleted = false
            },
            new()
            {
                Id = 4004,
                PostId = 1204,
                TotalVoteCount = 18,
                IsClosed = true,
                EndTime = now.AddMinutes(-5),
                IsDeleted = false
            }
        };

        var posts = new List<Post>
        {
            new(new PostInitializationOptions("最先截止", "正文"))
            {
                Id = 1201,
                IsPublished = true,
                IsDeleted = false,
                IsTop = false,
                CreateTime = now.AddMinutes(-20)
            },
            new(new PostInitializationOptions("较晚截止", "正文"))
            {
                Id = 1202,
                IsPublished = true,
                IsDeleted = false,
                IsTop = false,
                CreateTime = now.AddMinutes(-10)
            },
            new(new PostInitializationOptions("长期有效", "正文"))
            {
                Id = 1203,
                IsPublished = true,
                IsDeleted = false,
                IsTop = false,
                CreateTime = now.AddMinutes(-5)
            },
            new(new PostInitializationOptions("已经截止", "正文"))
            {
                Id = 1204,
                IsPublished = true,
                IsDeleted = false,
                IsTop = false,
                CreateTime = now.AddMinutes(-1)
            }
        };

        postPollRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync((Expression<Func<PostPoll, bool>>? expression) =>
            {
                var predicate = expression?.Compile() ?? (_ => true);
                return polls.Where(predicate).ToList();
            });
        postRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Post, bool>>?>()))
            .ReturnsAsync((Expression<Func<Post, bool>>? expression) =>
            {
                var predicate = expression?.Compile() ?? (_ => true);
                return posts.Where(predicate).ToList();
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
            CreateDefaultSystemSettingProvider());

        var (data, totalCount) = await service.GetPollPostPageAsync(sortBy: "deadline");

        Assert.Equal(4, totalCount);
        Assert.Equal([1201L, 1202L, 1203L, 1204L], data.Select(post => post.VoId).ToArray());

        postPollRepository.Verify(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostPoll, bool>>?>()), Times.Once);
        postRepository.Verify(repository => repository.QueryAsync(It.IsAny<Expression<Func<Post, bool>>?>()), Times.Once);
        postRepository.Verify(repository => repository.QueryPageAsync(
            It.IsAny<Expression<Func<Post, bool>>>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<Expression<Func<Post, object>>>(),
            It.IsAny<OrderByType>()), Times.Never);
    }
}
