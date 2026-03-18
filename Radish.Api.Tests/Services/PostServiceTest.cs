using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.OptionTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public class PostServiceTest
{
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
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

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
                    IsEnabled = true,
                    IsDeleted = false
                },
                new Tag("社区")
                {
                    Id = 502,
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
            mapper.Object,
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
            Options.Create(new ForumEditHistoryOptions()));

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
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var post = new Post(new PostInitializationOptions("问答详情帖", "这个问题怎么解？")
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
        mapper
            .Setup(m => m.Map<PostVo>(post))
            .Returns(new PostVo
            {
                VoId = 1004,
                VoTitle = "问答详情帖",
                VoContent = "这个问题怎么解？",
                VoAuthorId = 9527,
                VoAuthorName = "Tester"
            });
        mapper
            .Setup(m => m.Map<PostQuestionVo>(question))
            .Returns(new PostQuestionVo
            {
                VoPostId = 1004,
                VoIsSolved = false,
                VoAcceptedAnswerId = null,
                VoAnswerCount = 3
            });

        var service = new PostService(
            mapper.Object,
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
            Options.Create(new ForumEditHistoryOptions()));

        var result = await service.GetPostDetailAsync(1004, viewerUserId: null);

        Assert.NotNull(result);
        Assert.True(result!.VoIsQuestion);
        Assert.False(result.VoIsSolved);
        Assert.Equal(3, result.VoAnswerCount);
        Assert.NotNull(result.VoQuestion);
        Assert.Equal(1004, result.VoQuestion!.VoPostId);
        Assert.Equal(3, result.VoQuestion.VoAnswerCount);
        Assert.Empty(result.VoQuestion.VoAnswers);
        Assert.Null(result.VoPoll);

        postRepository.VerifyAll();
        postTagRepository.VerifyAll();
        postPollRepository.VerifyAll();
        postQuestionRepository.VerifyAll();
        postAnswerRepository.VerifyAll();
        mapper.VerifyAll();
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
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

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
        mapper
            .Setup(m => m.Map<PostVo>(post))
            .Returns(new PostVo
            {
                VoId = 1005,
                VoTitle = "问答详情帖",
                VoContent = "看看回答列表",
                VoAuthorId = 9527,
                VoAuthorName = "Tester"
            });
        mapper
            .Setup(m => m.Map<PostQuestionVo>(question))
            .Returns(new PostQuestionVo
            {
                VoPostId = 1005,
                VoIsSolved = false,
                VoAnswerCount = 2
            });
        mapper
            .Setup(m => m.Map<PostAnswerVo>(It.Is<PostAnswer>(answer => answer.Id == 4002)))
            .Returns(new PostAnswerVo
            {
                VoAnswerId = 4002,
                VoPostId = 1005,
                VoAuthorId = 2002,
                VoAuthorName = "Alice",
                VoContent = "先检查配置。",
                VoIsAccepted = true
            });
        mapper
            .Setup(m => m.Map<PostAnswerVo>(It.Is<PostAnswer>(answer => answer.Id == 4001)))
            .Returns(new PostAnswerVo
            {
                VoAnswerId = 4001,
                VoPostId = 1005,
                VoAuthorId = 2001,
                VoAuthorName = "Bob",
                VoContent = "可以先看日志。",
                VoIsAccepted = false
            });

        var service = new PostService(
            mapper.Object,
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
            Options.Create(new ForumEditHistoryOptions()));

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
        mapper.VerifyAll();
    }

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
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        postPollRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostPoll, bool>>?>()))
            .ReturnsAsync(new List<PostPoll>());

        var service = new PostService(
            mapper.Object,
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
            Options.Create(new ForumEditHistoryOptions()));

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
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

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
        mapper
            .Setup(m => m.Map<List<PostVo>>(It.IsAny<List<Post>>()))
            .Returns((List<Post> source) => source.Select(post => new PostVo
            {
                VoId = post.Id,
                VoTitle = post.Title,
                VoCreateTime = post.CreateTime
            }).ToList());

        var service = new PostService(
            mapper.Object,
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
            Options.Create(new ForumEditHistoryOptions()));

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
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

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
        mapper
            .Setup(m => m.Map<List<PostVo>>(It.IsAny<List<Post>>()))
            .Returns((List<Post> source) => source.Select(post => new PostVo
            {
                VoId = post.Id,
                VoTitle = post.Title,
                VoIsTop = post.IsTop,
                VoCreateTime = post.CreateTime
            }).ToList());

        var service = new PostService(
            mapper.Object,
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
            Options.Create(new ForumEditHistoryOptions()));

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
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

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
        mapper
            .Setup(m => m.Map<List<PostVo>>(It.IsAny<List<Post>>()))
            .Returns((List<Post> source) => source.Select(post => new PostVo
            {
                VoId = post.Id,
                VoTitle = post.Title,
                VoIsTop = post.IsTop,
                VoCreateTime = post.CreateTime
            }).ToList());

        var service = new PostService(
            mapper.Object,
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
            Options.Create(new ForumEditHistoryOptions()));

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
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

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
        mapper
            .Setup(m => m.Map<PostQuestionVo>(It.Is<PostQuestion>(item => item.PostId == 1006 && item.AnswerCount == 1)))
            .Returns(new PostQuestionVo
            {
                VoPostId = 1006,
                VoIsSolved = false,
                VoAnswerCount = 1
            });
        mapper
            .Setup(m => m.Map<PostAnswerVo>(It.Is<PostAnswer>(answer => answer.Id == 4003)))
            .Returns(new PostAnswerVo
            {
                VoAnswerId = 4003,
                VoPostId = 1006,
                VoAuthorId = 2001,
                VoAuthorName = "Alice",
                VoContent = "先检查数据库连接。",
                VoIsAccepted = false
            });

        var service = new PostService(
            mapper.Object,
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
            Options.Create(new ForumEditHistoryOptions()));

        var result = await service.AddAnswerAsync(1006, "  先检查数据库连接。  ", 2001, "Alice", 9);

        Assert.Equal(1006, result.VoPostId);
        Assert.Equal(1, result.VoAnswerCount);
        Assert.Single(result.VoAnswers);
        Assert.Equal("先检查数据库连接。", result.VoAnswers[0].VoContent);

        postRepository.VerifyAll();
        postQuestionRepository.VerifyAll();
        postAnswerRepository.VerifyAll();
        mapper.VerifyAll();
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
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

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
        mapper
            .Setup(m => m.Map<PostQuestionVo>(It.Is<PostQuestion>(item =>
                item.PostId == 1007 &&
                item.IsSolved &&
                item.AcceptedAnswerId == 4004)))
            .Returns(new PostQuestionVo
            {
                VoPostId = 1007,
                VoIsSolved = true,
                VoAcceptedAnswerId = 4004,
                VoAnswerCount = 2
            });
        mapper
            .Setup(m => m.Map<PostAnswerVo>(It.Is<PostAnswer>(item => item.Id == 4004 && item.IsAccepted)))
            .Returns(new PostAnswerVo
            {
                VoAnswerId = 4004,
                VoPostId = 1007,
                VoAuthorId = 2002,
                VoAuthorName = "Alice",
                VoContent = "这是正确答案",
                VoIsAccepted = true
            });

        var service = new PostService(
            mapper.Object,
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
            Options.Create(new ForumEditHistoryOptions()));

        var result = await service.AcceptAnswerAsync(1007, 4004, 9527, "Owner");

        Assert.True(result.VoIsSolved);
        Assert.Equal(4004, result.VoAcceptedAnswerId);
        Assert.Single(result.VoAnswers);
        Assert.True(result.VoAnswers[0].VoIsAccepted);

        postRepository.VerifyAll();
        postQuestionRepository.VerifyAll();
        postAnswerRepository.VerifyAll();
        mapper.VerifyAll();
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
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

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
            mapper.Object,
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
            Options.Create(new ForumEditHistoryOptions()));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AcceptAnswerAsync(1008, 4005, 9527, "Owner"));

        Assert.Equal("当前问题已采纳答案", exception.Message);

        postRepository.VerifyAll();
        postQuestionRepository.VerifyAll();
        postAnswerRepository.Verify(
            repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<PostAnswer, bool>>?>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdatePostAsync_Should_Create_EditHistory_When_QuestionPostIsUpdated()
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
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

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
            .Setup(repository => repository.UpdateAsync(It.Is<Post>(item =>
                item.Id == 2001 &&
                item.Title == "更新后的问题标题" &&
                item.Content == "更新后的问题内容" &&
                item.EditCount == 1 &&
                item.ModifyBy == "Owner" &&
                item.ModifyId == 9527)))
            .ReturnsAsync(true);

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

        postEditHistoryRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<PostEditHistory, bool>>?>()))
            .ReturnsAsync(0);
        postEditHistoryRepository
            .Setup(repository => repository.AddAsync(It.Is<PostEditHistory>(history =>
                history.PostId == 2001 &&
                history.EditSequence == 1 &&
                history.OldTitle == "原问题标题" &&
                history.NewTitle == "更新后的问题标题" &&
                history.OldContent == "原问题内容" &&
                history.NewContent == "更新后的问题内容" &&
                history.EditorId == 9527 &&
                history.EditorName == "Owner" &&
                history.TenantId == 9 &&
                history.CreateBy == "Owner" &&
                history.CreateId == 9527)))
            .ReturnsAsync(7001);
        postEditHistoryRepository
            .Setup(repository => repository.QueryWithOrderAsync(
                It.IsAny<Expression<Func<PostEditHistory, bool>>?>(),
                It.IsAny<Expression<Func<PostEditHistory, object>>>(),
                OrderByType.Desc,
                0))
            .ReturnsAsync(
            [
                new PostEditHistory
                {
                    Id = 7001,
                    PostId = 2001,
                    EditSequence = 1
                }
            ]);

        var service = new PostService(
            mapper.Object,
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
            }));

        await service.UpdatePostAsync(
            2001,
            "更新后的问题标题",
            "更新后的问题内容",
            null,
            ["问答"],
            allowCreateTag: false,
            operatorId: 9527,
            operatorName: "Owner");

        postRepository.VerifyAll();
        postTagRepository.VerifyAll();
        tagRepository.VerifyAll();
        postEditHistoryRepository.VerifyAll();
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
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

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
        mapper
            .Setup(m => m.Map<List<PostEditHistoryVo>>(histories))
            .Returns(historyVos);

        var service = new PostService(
            mapper.Object,
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
            Options.Create(new ForumEditHistoryOptions()));

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
        mapper.VerifyAll();
    }

    [Fact]
    public async Task PublishPostAsync_Should_CreatePoll_AndTagRelations_When_PollIsValid()
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
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var tags = new List<Tag>
        {
            new("投票")
            {
                Id = 501,
                IsEnabled = true,
                IsDeleted = false,
                PostCount = 0
            },
            new("社区")
            {
                Id = 502,
                IsEnabled = true,
                IsDeleted = false,
                PostCount = 3
            }
        };

        postRepository
            .Setup(repository => repository.AddAsync(It.Is<Post>(post =>
                post.AuthorId == 9527 &&
                post.AuthorName == "Tester" &&
                post.TenantId == 9 &&
                post.CategoryId == 0)))
            .ReturnsAsync(1001);
        postRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(2);

        postTagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>?>()))
            .ReturnsAsync(new List<PostTag>());
        postTagRepository
            .Setup(repository => repository.AddAsync(It.IsAny<PostTag>()))
            .ReturnsAsync((PostTag relation) => relation.TagId == 501 ? 9101 : 9102);

        tagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Tag, bool>>?>()))
            .ReturnsAsync((Expression<Func<Tag, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return tags;
                }

                var predicate = expression.Compile();
                return tags.Where(predicate).ToList();
            });
        tagRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<Tag>()))
            .ReturnsAsync(true);

        postPollRepository
            .Setup(repository => repository.AddAsync(It.Is<PostPoll>(poll =>
                poll.PostId == 1001 &&
                poll.Question == "中午吃什么？" &&
                poll.TenantId == 9 &&
                poll.CreateBy == "Tester" &&
                poll.CreateId == 9527 &&
                poll.TotalVoteCount == 0 &&
                !poll.IsClosed)))
            .ReturnsAsync(2001);

        postPollOptionRepository
            .Setup(repository => repository.AddRangeAsync(It.Is<List<PostPollOption>>(options =>
                options.Count == 2 &&
                options[0].PollId == 2001 &&
                options[0].OptionText == "盖饭" &&
                options[0].SortOrder == 1 &&
                options[0].TenantId == 9 &&
                options[1].PollId == 2001 &&
                options[1].OptionText == "面条" &&
                options[1].SortOrder == 5 &&
                options[1].TenantId == 9)))
            .ReturnsAsync(2);

        experienceService
            .Setup(service => service.GrantExperienceAsync(
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(true);

        var service = new PostService(
            mapper.Object,
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
            Options.Create(new ForumEditHistoryOptions()));

        var post = new Post(new PostInitializationOptions("投票帖", "今天吃什么")
        {
            AuthorId = 9527,
            AuthorName = "Tester",
            TenantId = 9,
            CategoryId = 0,
            IsPublished = true
        });

        var result = await service.PublishPostAsync(
            post,
            new CreatePollDto
            {
                Question = "  中午吃什么？  ",
                EndTime = DateTime.UtcNow.AddHours(4),
                Options =
                [
                    new PollOptionDto { OptionText = "  盖饭  " },
                    new PollOptionDto { OptionText = "面条", SortOrder = 5 }
                ]
            },
            null,
            false,
            ["投票", "社区"],
            allowCreateTag: false);

        Assert.Equal(1001, result);

        postRepository.Verify(repository => repository.AddAsync(It.IsAny<Post>()), Times.Once);
        postTagRepository.Verify(repository => repository.AddAsync(It.IsAny<PostTag>()), Times.Exactly(2));
        tagRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Tag>()), Times.Exactly(2));
        postPollRepository.Verify(repository => repository.AddAsync(It.IsAny<PostPoll>()), Times.Once);
        postPollOptionRepository.Verify(repository => repository.AddRangeAsync(It.IsAny<List<PostPollOption>>()), Times.Once);
    }

    [Fact]
    public async Task PublishPostAsync_Should_AcceptUtcEndTime_A_FewMinutesInFuture()
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
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var tags = new List<Tag>
        {
            new("投票")
            {
                Id = 501,
                IsEnabled = true,
                IsDeleted = false
            }
        };

        var utcEndTime = DateTime.UtcNow.AddMinutes(3);

        postRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Post>()))
            .ReturnsAsync(1002);
        postRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(2);

        postTagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>?>()))
            .ReturnsAsync(new List<PostTag>());
        postTagRepository
            .Setup(repository => repository.AddAsync(It.IsAny<PostTag>()))
            .ReturnsAsync(9103);

        tagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Tag, bool>>?>()))
            .ReturnsAsync((Expression<Func<Tag, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return tags;
                }

                var predicate = expression.Compile();
                return tags.Where(predicate).ToList();
            });
        tagRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<Tag>()))
            .ReturnsAsync(true);

        postPollRepository
            .Setup(repository => repository.AddAsync(It.Is<PostPoll>(poll =>
                poll.PostId == 1002 &&
                poll.EndTime == utcEndTime)))
            .ReturnsAsync(2002);

        postPollOptionRepository
            .Setup(repository => repository.AddRangeAsync(It.IsAny<List<PostPollOption>>()))
            .ReturnsAsync(2);

        experienceService
            .Setup(service => service.GrantExperienceAsync(
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(true);

        var service = new PostService(
            mapper.Object,
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
            Options.Create(new ForumEditHistoryOptions()));

        var post = new Post(new PostInitializationOptions("短时投票帖", "三分钟后截止")
        {
            AuthorId = 9527,
            AuthorName = "Tester",
            TenantId = 9,
            IsPublished = true
        });

        var result = await service.PublishPostAsync(
            post,
            new CreatePollDto
            {
                Question = "三分钟后吃什么？",
                EndTime = utcEndTime,
                Options =
                [
                    new PollOptionDto { OptionText = "米饭" },
                    new PollOptionDto { OptionText = "面" }
                ]
            },
            null,
            false,
            ["投票"],
            allowCreateTag: false);

        Assert.Equal(1002, result);
        postPollRepository.Verify(repository => repository.AddAsync(It.IsAny<PostPoll>()), Times.Once);
        postPollOptionRepository.Verify(repository => repository.AddRangeAsync(It.IsAny<List<PostPollOption>>()), Times.Once);
    }

    [Fact]
    public async Task PublishPostAsync_Should_CreateQuestionMarker_When_IsQuestionIsTrue()
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
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var tags = new List<Tag>
        {
            new("问答")
            {
                Id = 601,
                IsEnabled = true,
                IsDeleted = false
            }
        };

        postRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Post>()))
            .ReturnsAsync(1003);
        postRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(2);

        postTagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>?>()))
            .ReturnsAsync(new List<PostTag>());
        postTagRepository
            .Setup(repository => repository.AddAsync(It.IsAny<PostTag>()))
            .ReturnsAsync(9201);

        tagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Tag, bool>>?>()))
            .ReturnsAsync((Expression<Func<Tag, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return tags;
                }

                var predicate = expression.Compile();
                return tags.Where(predicate).ToList();
            });
        tagRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<Tag>()))
            .ReturnsAsync(true);

        postQuestionRepository
            .Setup(repository => repository.AddAsync(It.Is<PostQuestion>(question =>
                question.PostId == 1003 &&
                !question.IsSolved &&
                question.AcceptedAnswerId == null &&
                question.AnswerCount == 0 &&
                question.TenantId == 9 &&
                question.CreateBy == "Tester" &&
                question.CreateId == 9527)))
            .ReturnsAsync(3001);

        experienceService
            .Setup(service => service.GrantExperienceAsync(
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(true);

        var service = new PostService(
            mapper.Object,
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
            Options.Create(new ForumEditHistoryOptions()));

        var post = new Post(new PostInitializationOptions("问答帖", "这个问题怎么解？")
        {
            AuthorId = 9527,
            AuthorName = "Tester",
            TenantId = 9,
            IsPublished = true
        });

        var result = await service.PublishPostAsync(
            post,
            null,
            null,
            true,
            ["问答"],
            allowCreateTag: false);

        Assert.Equal(1003, result);
        postQuestionRepository.Verify(repository => repository.AddAsync(It.IsAny<PostQuestion>()), Times.Once);
        postPollRepository.Verify(repository => repository.AddAsync(It.IsAny<PostPoll>()), Times.Never);
    }

    [Fact]
    public async Task PublishPostAsync_Should_CreateLottery_When_LotteryIsValid()
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
        var postLotteryRepository = new Mock<IBaseRepository<PostLottery>>(MockBehavior.Strict);
        var tagService = new Mock<ITagService>(MockBehavior.Strict);
        var coinRewardService = new Mock<ICoinRewardService>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var dedupService = new Mock<INotificationDedupService>(MockBehavior.Strict);
        var experienceService = new Mock<IExperienceService>(MockBehavior.Strict);
        var postEditHistoryRepository = new Mock<IBaseRepository<PostEditHistory>>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var tags = new List<Tag>
        {
            new("抽奖")
            {
                Id = 701,
                IsEnabled = true,
                IsDeleted = false,
                PostCount = 0
            }
        };

        postRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Post>()))
            .ReturnsAsync(1004);
        postRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(2);

        postTagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>?>()))
            .ReturnsAsync(new List<PostTag>());
        postTagRepository
            .Setup(repository => repository.AddAsync(It.IsAny<PostTag>()))
            .ReturnsAsync(9301);

        tagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Tag, bool>>?>()))
            .ReturnsAsync((Expression<Func<Tag, bool>>? expression) =>
            {
                if (expression == null)
                {
                    return tags;
                }

                var predicate = expression.Compile();
                return tags.Where(predicate).ToList();
            });
        tagRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<Tag>()))
            .ReturnsAsync(true);

        postLotteryRepository
            .Setup(repository => repository.AddAsync(It.Is<PostLottery>(lottery =>
                lottery.PostId == 1004 &&
                lottery.PrizeName == "萝卜周边" &&
                lottery.PrizeDescription == "抽一个小礼包" &&
                lottery.WinnerCount == 2 &&
                lottery.ParticipantCount == 0 &&
                !lottery.IsDrawn &&
                lottery.TenantId == 9 &&
                lottery.CreateBy == "Tester" &&
                lottery.CreateId == 9527 &&
                lottery.DrawTime.HasValue)))
            .ReturnsAsync(4001);

        experienceService
            .Setup(service => service.GrantExperienceAsync(
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(true);

        var service = new PostService(
            mapper.Object,
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
            Options.Create(new ForumEditHistoryOptions()),
            postLotteryRepository: postLotteryRepository.Object);

        var post = new Post(new PostInitializationOptions("抽奖帖", "评论参与抽奖")
        {
            AuthorId = 9527,
            AuthorName = "Tester",
            TenantId = 9,
            IsPublished = true
        });

        var result = await service.PublishPostAsync(
            post,
            null,
            new CreateLotteryDto
            {
                PrizeName = "  萝卜周边  ",
                PrizeDescription = "  抽一个小礼包  ",
                WinnerCount = 2,
                DrawTime = DateTime.UtcNow.AddHours(2)
            },
            false,
            ["抽奖"],
            allowCreateTag: false);

        Assert.Equal(1004, result);
        postLotteryRepository.Verify(repository => repository.AddAsync(It.IsAny<PostLottery>()), Times.Once);
        postPollRepository.Verify(repository => repository.AddAsync(It.IsAny<PostPoll>()), Times.Never);
        postQuestionRepository.Verify(repository => repository.AddAsync(It.IsAny<PostQuestion>()), Times.Never);
    }

    [Fact]
    public async Task PublishPostAsync_Should_Reject_When_LotteryAndQuestionAreBothEnabled()
    {
        var service = new PostService(
            new Mock<IMapper>(MockBehavior.Strict).Object,
            new Mock<IBaseRepository<Post>>(MockBehavior.Strict).Object,
            new Mock<IBaseRepository<UserPostLike>>(MockBehavior.Strict).Object,
            new Mock<IBaseRepository<PostTag>>(MockBehavior.Strict).Object,
            new Mock<IBaseRepository<Category>>(MockBehavior.Strict).Object,
            new Mock<IBaseRepository<Tag>>(MockBehavior.Strict).Object,
            new Mock<IBaseRepository<PostPoll>>(MockBehavior.Strict).Object,
            new Mock<IBaseRepository<PostPollOption>>(MockBehavior.Strict).Object,
            new Mock<IBaseRepository<PostPollVote>>(MockBehavior.Strict).Object,
            new Mock<IBaseRepository<PostQuestion>>(MockBehavior.Strict).Object,
            new Mock<IBaseRepository<PostAnswer>>(MockBehavior.Strict).Object,
            new Mock<ITagService>(MockBehavior.Strict).Object,
            new Mock<ICoinRewardService>(MockBehavior.Strict).Object,
            new Mock<INotificationService>(MockBehavior.Strict).Object,
            new Mock<INotificationDedupService>(MockBehavior.Strict).Object,
            new Mock<IExperienceService>(MockBehavior.Strict).Object,
            new Mock<IBaseRepository<PostEditHistory>>(MockBehavior.Strict).Object,
            Options.Create(new ForumEditHistoryOptions()));

        var post = new Post(new PostInitializationOptions("冲突功能帖", "正文"))
        {
            AuthorId = 9527,
            AuthorName = "Tester",
            TenantId = 9,
            IsPublished = true
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.PublishPostAsync(
            post,
            null,
            new CreateLotteryDto
            {
                PrizeName = "萝卜周边",
                WinnerCount = 1,
                DrawTime = DateTime.UtcNow.AddHours(1)
            },
            true,
            ["抽奖"],
            allowCreateTag: false));

        Assert.Equal("问答帖、投票和抽奖暂时互斥", exception.Message);
    }
}
