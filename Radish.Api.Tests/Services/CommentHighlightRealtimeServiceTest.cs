using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.CacheTool;
using Radish.Common.OptionTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using Shouldly;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public class CommentHighlightRealtimeServiceTest
{
    [Fact(DisplayName = "实时重算神评时最高点赞为零不应创建神评")]
    public async Task TriggerHighlightRecheckAsync_ShouldNotCreateGodComment_WhenTopLikeCountIsZero()
    {
        var commentRepository = new Mock<IBaseRepository<Comment>>();
        var highlightRepository = new Mock<IBaseRepository<CommentHighlight>>();
        var caching = new Mock<ICaching>();
        const long postId = 9001;

        commentRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
            .ReturnsAsync(6);

        commentRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<Comment, bool>>?>(),
                1,
                5,
                It.IsAny<Expression<Func<Comment, object>>?>(),
                OrderByType.Desc,
                It.IsAny<Expression<Func<Comment, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((new List<Comment>
            {
                new()
                {
                    Id = 1001,
                    PostId = postId,
                    ParentId = null,
                    LikeCount = 0,
                    IsDeleted = false,
                    IsEnabled = true,
                    CreateTime = DateTime.Now
                }
            }, 6));

        highlightRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<CommentHighlight, bool>>?>()))
            .ReturnsAsync([]);

        var service = CreateService(commentRepository, highlightRepository, caching);

        var result = await service.TriggerHighlightRecheckAsync(postId);

        result.VoChanged.ShouldBeFalse();
        result.VoCurrentCommentIds.ShouldBeEmpty();
        highlightRepository.Verify(
            repository => repository.AddRangeAsync(It.IsAny<List<CommentHighlight>>()),
            Times.Never);
        highlightRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<CommentHighlight, CommentHighlight>>>(),
                It.IsAny<Expression<Func<CommentHighlight, bool>>>()),
            Times.Never);
        caching.Verify(cache => cache.RemoveAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "实时重算神评时最高点赞为零应清理旧神评")]
    public async Task TriggerHighlightRecheckAsync_ShouldClearCurrentGodComment_WhenTopLikeCountDropsToZero()
    {
        var commentRepository = new Mock<IBaseRepository<Comment>>();
        var highlightRepository = new Mock<IBaseRepository<CommentHighlight>>();
        var caching = new Mock<ICaching>();
        const long postId = 9002;
        var existingHighlight = new CommentHighlight
        {
            Id = 2001,
            PostId = postId,
            CommentId = 3001,
            HighlightType = 1,
            LikeCount = 1,
            IsCurrent = true,
            CreateTime = DateTime.Now
        };

        commentRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
            .ReturnsAsync(6);

        commentRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<Comment, bool>>?>(),
                1,
                5,
                It.IsAny<Expression<Func<Comment, object>>?>(),
                OrderByType.Desc,
                It.IsAny<Expression<Func<Comment, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((new List<Comment>
            {
                new()
                {
                    Id = existingHighlight.CommentId,
                    PostId = postId,
                    ParentId = null,
                    LikeCount = 0,
                    IsDeleted = false,
                    IsEnabled = true,
                    CreateTime = DateTime.Now
                }
            }, 6));

        highlightRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<CommentHighlight, bool>>?>()))
            .ReturnsAsync([existingHighlight]);

        highlightRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<CommentHighlight, CommentHighlight>>>(),
                It.IsAny<Expression<Func<CommentHighlight, bool>>>()))
            .ReturnsAsync(1);

        caching
            .Setup(cache => cache.RemoveAsync($"god_comments:post:{postId}"))
            .Returns(Task.CompletedTask);

        var service = CreateService(commentRepository, highlightRepository, caching);

        var result = await service.TriggerHighlightRecheckAsync(postId);

        result.VoChanged.ShouldBeTrue();
        result.VoCurrentCommentIds.ShouldBeEmpty();
        highlightRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<CommentHighlight, CommentHighlight>>>(),
                It.IsAny<Expression<Func<CommentHighlight, bool>>>()),
            Times.Once);
        caching.Verify(cache => cache.RemoveAsync($"god_comments:post:{postId}"), Times.Once);
        highlightRepository.Verify(
            repository => repository.AddRangeAsync(It.IsAny<List<CommentHighlight>>()),
            Times.Never);
    }

    [Fact(DisplayName = "实时重算沙发时最高点赞为零应清理旧沙发")]
    public async Task TriggerHighlightRecheckAsync_ShouldClearCurrentSofa_WhenTopLikeCountDropsToZero()
    {
        var commentRepository = new Mock<IBaseRepository<Comment>>();
        var highlightRepository = new Mock<IBaseRepository<CommentHighlight>>();
        var caching = new Mock<ICaching>();
        const long postId = 9003;
        const long parentCommentId = 9101;
        var existingHighlight = new CommentHighlight
        {
            Id = 2201,
            PostId = postId,
            CommentId = 3201,
            ParentCommentId = parentCommentId,
            HighlightType = 2,
            LikeCount = 1,
            IsCurrent = true,
            CreateTime = DateTime.Now
        };

        commentRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
            .ReturnsAsync(4);

        commentRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<Comment, bool>>?>(),
                1,
                5,
                It.IsAny<Expression<Func<Comment, object>>?>(),
                OrderByType.Desc,
                It.IsAny<Expression<Func<Comment, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((new List<Comment>
            {
                new()
                {
                    Id = existingHighlight.CommentId,
                    PostId = postId,
                    ParentId = parentCommentId,
                    LikeCount = 0,
                    IsDeleted = false,
                    IsEnabled = true,
                    CreateTime = DateTime.Now
                }
            }, 4));

        highlightRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<CommentHighlight, bool>>?>()))
            .ReturnsAsync([existingHighlight]);

        highlightRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<CommentHighlight, CommentHighlight>>>(),
                It.IsAny<Expression<Func<CommentHighlight, bool>>>()))
            .ReturnsAsync(1);

        caching
            .Setup(cache => cache.RemoveAsync($"sofas:parent:{parentCommentId}"))
            .Returns(Task.CompletedTask);

        var service = CreateService(commentRepository, highlightRepository, caching);

        var result = await service.TriggerHighlightRecheckAsync(postId, parentCommentId);

        result.VoChanged.ShouldBeTrue();
        result.VoCurrentCommentIds.ShouldBeEmpty();
        highlightRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<CommentHighlight, CommentHighlight>>>(),
                It.IsAny<Expression<Func<CommentHighlight, bool>>>()),
            Times.Once);
        caching.Verify(cache => cache.RemoveAsync($"sofas:parent:{parentCommentId}"), Times.Once);
        highlightRepository.Verify(
            repository => repository.AddRangeAsync(It.IsAny<List<CommentHighlight>>()),
            Times.Never);
    }

    [Fact(DisplayName = "稳定窗口内新候选未达到点赞领先阈值时应保留当前神评")]
    public async Task TriggerHighlightRecheckAsync_ShouldKeepCurrentGodComment_WhenLeadIsBelowConfiguredDelta()
    {
        var commentRepository = new Mock<IBaseRepository<Comment>>();
        var highlightRepository = new Mock<IBaseRepository<CommentHighlight>>();
        var caching = new Mock<ICaching>();
        const long postId = 9004;
        var existingHighlight = new CommentHighlight
        {
            Id = 2401,
            PostId = postId,
            CommentId = 3401,
            HighlightType = 1,
            LikeCount = 5,
            IsCurrent = true,
            CreateTime = DateTime.Now.AddMinutes(-1)
        };

        commentRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
            .ReturnsAsync(6);

        commentRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<Comment, bool>>?>(),
                1,
                5,
                It.IsAny<Expression<Func<Comment, object>>?>(),
                OrderByType.Desc,
                It.IsAny<Expression<Func<Comment, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((new List<Comment>
            {
                new()
                {
                    Id = 3402,
                    PostId = postId,
                    ParentId = null,
                    LikeCount = 6,
                    IsDeleted = false,
                    IsEnabled = true,
                    CreateTime = DateTime.Now
                }
            }, 6));

        highlightRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<CommentHighlight, bool>>?>()))
            .ReturnsAsync([existingHighlight]);

        var service = CreateService(
            commentRepository,
            highlightRepository,
            caching,
            commentHighlightStabilityWindowMinutes: 10,
            commentHighlightReplacementMinLikeDelta: 2);

        var result = await service.TriggerHighlightRecheckAsync(postId);

        result.VoChanged.ShouldBeFalse();
        Assert.Single(result.VoCurrentCommentIds);
        Assert.Equal(existingHighlight.CommentId, result.VoCurrentCommentIds[0]);
        highlightRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<CommentHighlight, CommentHighlight>>>(),
                It.IsAny<Expression<Func<CommentHighlight, bool>>>()),
            Times.Never);
        highlightRepository.Verify(
            repository => repository.AddRangeAsync(It.IsAny<List<CommentHighlight>>()),
            Times.Never);
        caching.Verify(cache => cache.RemoveAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "稳定窗口内新候选达到点赞领先阈值时应替换当前神评")]
    public async Task TriggerHighlightRecheckAsync_ShouldReplaceCurrentGodComment_WhenLeadMeetsConfiguredDelta()
    {
        var commentRepository = new Mock<IBaseRepository<Comment>>();
        var highlightRepository = new Mock<IBaseRepository<CommentHighlight>>();
        var caching = new Mock<ICaching>();
        const long postId = 9005;
        var existingHighlight = new CommentHighlight
        {
            Id = 2501,
            PostId = postId,
            CommentId = 3501,
            HighlightType = 1,
            LikeCount = 5,
            IsCurrent = true,
            CreateTime = DateTime.Now.AddMinutes(-1)
        };
        var capturedHighlights = new List<CommentHighlight>();

        commentRepository
            .Setup(repository => repository.QueryCountAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
            .ReturnsAsync(6);

        commentRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<Comment, bool>>?>(),
                1,
                5,
                It.IsAny<Expression<Func<Comment, object>>?>(),
                OrderByType.Desc,
                It.IsAny<Expression<Func<Comment, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((new List<Comment>
            {
                new()
                {
                    Id = 3502,
                    PostId = postId,
                    ParentId = null,
                    LikeCount = 7,
                    Content = "new-highlight",
                    AuthorId = 4502,
                    AuthorName = "new-author",
                    TenantId = 1,
                    IsDeleted = false,
                    IsEnabled = true,
                    CreateTime = DateTime.Now
                }
            }, 6));

        highlightRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<CommentHighlight, bool>>?>()))
            .ReturnsAsync([existingHighlight]);

        highlightRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<CommentHighlight, CommentHighlight>>>(),
                It.IsAny<Expression<Func<CommentHighlight, bool>>>()))
            .ReturnsAsync(1);

        highlightRepository
            .Setup(repository => repository.AddRangeAsync(It.IsAny<List<CommentHighlight>>()))
            .Callback<List<CommentHighlight>>(items => capturedHighlights = items)
            .ReturnsAsync(1);

        caching
            .Setup(cache => cache.RemoveAsync($"god_comments:post:{postId}"))
            .Returns(Task.CompletedTask);

        var service = CreateService(
            commentRepository,
            highlightRepository,
            caching,
            commentHighlightStabilityWindowMinutes: 10,
            commentHighlightReplacementMinLikeDelta: 2);

        var result = await service.TriggerHighlightRecheckAsync(postId);

        result.VoChanged.ShouldBeTrue();
        Assert.Single(result.VoCurrentCommentIds);
        Assert.Equal(3502, result.VoCurrentCommentIds[0]);
        Assert.Single(capturedHighlights);
        Assert.Equal(3502, capturedHighlights[0].CommentId);
        Assert.Equal(7, capturedHighlights[0].LikeCount);
        highlightRepository.Verify(
            repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<CommentHighlight, CommentHighlight>>>(),
                It.IsAny<Expression<Func<CommentHighlight, bool>>>()),
            Times.Once);
        highlightRepository.Verify(
            repository => repository.AddRangeAsync(It.IsAny<List<CommentHighlight>>()),
            Times.Once);
        caching.Verify(cache => cache.RemoveAsync($"god_comments:post:{postId}"), Times.Once);
    }

    private static CommentService CreateService(
        Mock<IBaseRepository<Comment>> commentRepository,
        Mock<IBaseRepository<CommentHighlight>> highlightRepository,
        Mock<ICaching> caching,
        int commentHighlightStabilityWindowMinutes = 10,
        int commentHighlightReplacementMinLikeDelta = 2)
    {
        var mapper = new Mock<IMapper>();
        var userCommentLikeRepository = new Mock<IBaseRepository<UserCommentLike>>();
        var commentEditHistoryRepository = new Mock<IBaseRepository<CommentEditHistory>>();
        var postService = new Mock<IPostService>();
        var coinRewardService = new Mock<ICoinRewardService>();
        var notificationService = new Mock<INotificationService>();
        var dedupService = new Mock<INotificationDedupService>();
        var experienceService = new Mock<IExperienceService>();
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>();

        coinRewardService
            .Setup(service => service.GrantGodCommentRewardAsync(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<int>()))
            .ReturnsAsync(CoinRewardResult.Success("test-god-comment", 0));
        coinRewardService
            .Setup(service => service.GrantSofaRewardAsync(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<int>()))
            .ReturnsAsync(CoinRewardResult.Success("test-sofa", 0));
        coinRewardService
            .Setup(service => service.GrantLikeBonusRewardAsync(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<string>()))
            .ReturnsAsync(CoinRewardResult.Success("test-like-bonus", 0));
        experienceService
            .Setup(service => service.HasExperienceTransactionAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<long>()))
            .ReturnsAsync(true);

        return new CommentService(
            mapper.Object,
            commentRepository.Object,
            userCommentLikeRepository.Object,
            highlightRepository.Object,
            postService.Object,
            caching.Object,
            coinRewardService.Object,
            notificationService.Object,
            dedupService.Object,
            experienceService.Object,
            attachmentUrlResolver.Object,
            Options.Create(new CommentHighlightOptions()),
            commentEditHistoryRepository.Object,
            Options.Create(new ForumEditHistoryOptions()),
            CreateDefaultSystemSettingProvider(
                commentHighlightStabilityWindowMinutes,
                commentHighlightReplacementMinLikeDelta),
            reliableOutboxService: Mock.Of<IReliableOutboxService>());
    }

    private static ISystemSettingProvider CreateDefaultSystemSettingProvider(
        int commentHighlightStabilityWindowMinutes = 10,
        int commentHighlightReplacementMinLikeDelta = 2)
    {
        var provider = new Mock<ISystemSettingProvider>();
        provider
            .Setup(item => item.GetInt32Async(SystemConfigDefaults.CommentBodyMinLengthKey))
            .ReturnsAsync(int.Parse(SystemConfigDefaults.DefaultCommentBodyMinLength));
        provider
            .Setup(item => item.GetInt32Async(SystemConfigDefaults.CommentBodyMaxLengthKey))
            .ReturnsAsync(int.Parse(SystemConfigDefaults.DefaultCommentBodyMaxLength));
        provider
            .Setup(item => item.GetInt32Async(SystemConfigDefaults.CommentHighlightStabilityWindowMinutesKey))
            .ReturnsAsync(commentHighlightStabilityWindowMinutes);
        provider
            .Setup(item => item.GetInt32Async(SystemConfigDefaults.CommentHighlightReplacementMinLikeDeltaKey))
            .ReturnsAsync(commentHighlightReplacementMinLikeDelta);

        return provider.Object;
    }
}
