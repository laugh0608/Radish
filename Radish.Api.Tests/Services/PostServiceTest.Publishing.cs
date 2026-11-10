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
            reliableOutboxService: Mock.Of<IReliableOutboxService>());

        var post = new Post(new PostInitializationOptions("投票帖", "今天中午大家一起吃什么比较合适")
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
            reliableOutboxService: Mock.Of<IReliableOutboxService>());

        var post = new Post(new PostInitializationOptions("短时投票帖", "三分钟后截止的短时投票正文")
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
    public async Task PublishPostAsync_Should_Return_StableForbiddenContract_When_TagCreationIsNotAllowed()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var postTagRepository = new Mock<IBaseRepository<PostTag>>(MockBehavior.Strict);
        var tagRepository = new Mock<IBaseRepository<Tag>>(MockBehavior.Strict);
        postRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Post>()))
            .ReturnsAsync(1002);
        postTagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>?>()))
            .ReturnsAsync([]);
        tagRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Tag, bool>>?>()))
            .ReturnsAsync([]);

        var service = new PostService(
            postRepository.Object,
            Mock.Of<IBaseRepository<UserPostLike>>(),
            postTagRepository.Object,
            Mock.Of<IBaseRepository<Category>>(),
            tagRepository.Object,
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
        var post = new Post(new PostInitializationOptions("标签权限", "普通成员不能隐式创建不存在的标签")
        {
            AuthorId = 9527,
            AuthorName = "Tester",
            TenantId = 9,
            IsPublished = true
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.PublishPostAsync(
            post,
            tagNames: ["不存在的标签"],
            allowCreateTag: false));

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal("Forum.PublishTagCreateForbidden", exception.ErrorCode);
        Assert.Equal("error.forum.publish_tag_create_forbidden", exception.MessageKey);
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
            reliableOutboxService: Mock.Of<IReliableOutboxService>());

        var post = new Post(new PostInitializationOptions("问答帖", "这个问题应该怎么分析和解决？")
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
            postLotteryRepository: postLotteryRepository.Object,
            reliableOutboxService: Mock.Of<IReliableOutboxService>());

        var post = new Post(new PostInitializationOptions("抽奖帖", "评论参与抽奖并说明参与理由")
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
    public async Task PublishPostAsync_Should_Reject_When_LotteryDrawTime_IsLessThanOneHourAway()
    {
        var service = new PostService(
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
            Mock.Of<IAttachmentService>(),
            Options.Create(new ForumEditHistoryOptions()),
            CreateDefaultSystemSettingProvider(),
            postLotteryRepository: new Mock<IBaseRepository<PostLottery>>(MockBehavior.Strict).Object);

        var post = new Post(new PostInitializationOptions("抽奖帖", "评论参与抽奖并说明参与理由"))
        {
            AuthorId = 9527,
            AuthorName = "Tester",
            TenantId = 9,
            IsPublished = true
        };

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.PublishPostAsync(
            post,
            null,
            new CreateLotteryDto
            {
                PrizeName = "萝卜周边",
                PrizeDescription = "抽一个小礼包",
                WinnerCount = 1,
                DrawTime = DateTime.UtcNow.AddMinutes(30)
            },
            false,
            ["抽奖"],
            allowCreateTag: false));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("Forum.PublishLotteryDrawTimeTooSoon", exception.ErrorCode);
        Assert.Equal("error.forum.publish_lottery_draw_time_too_soon", exception.MessageKey);
    }

    [Fact]
    public async Task PublishPostAsync_Should_Reject_When_LotteryAndQuestionAreBothEnabled()
    {
        var service = new PostService(
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
            Mock.Of<IAttachmentService>(),
            Options.Create(new ForumEditHistoryOptions()),
            CreateDefaultSystemSettingProvider());

        var post = new Post(new PostInitializationOptions("冲突功能帖", "正文"))
        {
            AuthorId = 9527,
            AuthorName = "Tester",
            TenantId = 9,
            IsPublished = true
        };

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.PublishPostAsync(
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

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("Forum.PublishFeatureCombinationInvalid", exception.ErrorCode);
        Assert.Equal("error.forum.publish_feature_combination_invalid", exception.MessageKey);
    }

    [Fact]
    public async Task PublishPostAsync_Should_Reject_When_TitleExceedsConfiguredMaxLength()
    {
        var service = new PostService(
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
            Mock.Of<IAttachmentService>(),
            Options.Create(new ForumEditHistoryOptions()),
            CreateDefaultSystemSettingProvider(postTitleMaxLength: 3));

        var post = new Post(new PostInitializationOptions("标题超长", "这是一段符合默认最小长度要求的正文"))
        {
            AuthorId = 9527,
            AuthorName = "Tester",
            TenantId = 9,
            IsPublished = true
        };

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.PublishPostAsync(
            post,
            tagNames: ["公告"],
            allowCreateTag: false));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("Forum.PublishTitleTooLong", exception.ErrorCode);
        Assert.Equal("error.forum.publish_title_too_long", exception.MessageKey);
    }
}
