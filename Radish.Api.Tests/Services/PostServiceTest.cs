using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.OptionTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
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
}
