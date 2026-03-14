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
            true,
            ["问答"],
            allowCreateTag: false);

        Assert.Equal(1003, result);
        postQuestionRepository.Verify(repository => repository.AddAsync(It.IsAny<PostQuestion>()), Times.Once);
        postPollRepository.Verify(repository => repository.AddAsync(It.IsAny<PostPoll>()), Times.Never);
    }
}
