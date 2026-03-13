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
                    EndTime = DateTime.Now.AddHours(2)
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

        Assert.False(posts[1].VoHasPoll);
        Assert.Equal(0, posts[1].VoPollTotalVoteCount);
        Assert.False(posts[1].VoPollIsClosed);

        postTagRepository.VerifyAll();
        categoryRepository.VerifyAll();
        tagRepository.VerifyAll();
        postPollRepository.VerifyAll();
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
                EndTime = DateTime.Now.AddHours(4),
                Options =
                [
                    new PollOptionDto { OptionText = "  盖饭  " },
                    new PollOptionDto { OptionText = "面条", SortOrder = 5 }
                ]
            },
            ["投票", "社区"],
            allowCreateTag: false);

        Assert.Equal(1001, result);

        postRepository.Verify(repository => repository.AddAsync(It.IsAny<Post>()), Times.Once);
        postTagRepository.Verify(repository => repository.AddAsync(It.IsAny<PostTag>()), Times.Exactly(2));
        tagRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Tag>()), Times.Exactly(2));
        postPollRepository.Verify(repository => repository.AddAsync(It.IsAny<PostPoll>()), Times.Once);
        postPollOptionRepository.Verify(repository => repository.AddRangeAsync(It.IsAny<List<PostPollOption>>()), Times.Once);
    }
}
