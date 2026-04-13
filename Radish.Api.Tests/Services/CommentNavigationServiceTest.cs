using System;
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
using Xunit;

namespace Radish.Api.Tests.Services;

/// <summary>
/// 评论精确定位服务测试
/// </summary>
public class CommentNavigationServiceTest
{
    [Fact(DisplayName = "根评论定位应返回正确根页码")]
    public async Task GetCommentNavigationAsync_ShouldReturnRootNavigation_WhenTargetIsRootComment()
    {
        var commentRepository = new Mock<IBaseRepository<Comment>>();
        var targetComment = new Comment
        {
            Id = 5001,
            PostId = 7001,
            ParentId = null,
            RootId = null,
            IsDeleted = false,
            IsEnabled = true,
            IsTop = false,
            CreateTime = new DateTime(2026, 4, 8, 12, 0, 0)
        };

        commentRepository
            .Setup(repo => repo.QueryByIdAsync(targetComment.Id))
            .ReturnsAsync(targetComment);

        commentRepository
            .Setup(repo => repo.QueryCountAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
            .ReturnsAsync(25);

        var service = CreateService(commentRepository);

        var result = await service.GetCommentNavigationAsync(targetComment.PostId, targetComment.Id, 20, 5);

        result.ShouldNotBeNull();
        result.VoCommentId.ShouldBe(targetComment.Id);
        result.VoPostId.ShouldBe(targetComment.PostId);
        result.VoRootCommentId.ShouldBe(targetComment.Id);
        result.VoParentCommentId.ShouldBeNull();
        result.VoIsRootComment.ShouldBeTrue();
        result.VoRootPageIndex.ShouldBe(2);
        result.VoChildPageIndex.ShouldBeNull();
    }

    [Fact(DisplayName = "子评论定位应返回正确根页码与子页码")]
    public async Task GetCommentNavigationAsync_ShouldReturnChildNavigation_WhenTargetIsChildComment()
    {
        var commentRepository = new Mock<IBaseRepository<Comment>>();
        var rootComment = new Comment
        {
            Id = 6001,
            PostId = 8001,
            ParentId = null,
            RootId = null,
            IsDeleted = false,
            IsEnabled = true,
            IsTop = false,
            CreateTime = new DateTime(2026, 4, 8, 10, 0, 0)
        };
        var targetComment = new Comment
        {
            Id = 6009,
            PostId = 8001,
            ParentId = 6101,
            RootId = rootComment.Id,
            IsDeleted = false,
            IsEnabled = true,
            LikeCount = 12,
            CreateTime = new DateTime(2026, 4, 8, 11, 0, 0)
        };

        commentRepository
            .Setup(repo => repo.QueryByIdAsync(targetComment.Id))
            .ReturnsAsync(targetComment);

        commentRepository
            .Setup(repo => repo.QueryByIdAsync(rootComment.Id))
            .ReturnsAsync(rootComment);

        commentRepository
            .SetupSequence(repo => repo.QueryCountAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
            .ReturnsAsync(21)
            .ReturnsAsync(7);

        var service = CreateService(commentRepository);

        var result = await service.GetCommentNavigationAsync(targetComment.PostId, targetComment.Id, 20, 5);

        result.ShouldNotBeNull();
        result.VoCommentId.ShouldBe(targetComment.Id);
        result.VoPostId.ShouldBe(targetComment.PostId);
        result.VoRootCommentId.ShouldBe(rootComment.Id);
        result.VoParentCommentId.ShouldBe(targetComment.ParentId);
        result.VoIsRootComment.ShouldBeFalse();
        result.VoRootPageIndex.ShouldBe(2);
        result.VoChildPageIndex.ShouldBe(2);
    }

    [Fact(DisplayName = "评论不存在时应返回空")]
    public async Task GetCommentNavigationAsync_ShouldReturnNull_WhenCommentDoesNotExist()
    {
        var commentRepository = new Mock<IBaseRepository<Comment>>();
        commentRepository
            .Setup(repo => repo.QueryByIdAsync(9001))
            .ReturnsAsync((Comment?)null);

        var service = CreateService(commentRepository);

        var result = await service.GetCommentNavigationAsync(7001, 9001, 20, 5);

        result.ShouldBeNull();
    }

    [Fact(DisplayName = "评论已删除时应返回空")]
    public async Task GetCommentNavigationAsync_ShouldReturnNull_WhenCommentDeleted()
    {
        var commentRepository = new Mock<IBaseRepository<Comment>>();
        var deletedComment = new Comment
        {
            Id = 9101,
            PostId = 7101,
            IsDeleted = true,
            IsEnabled = true
        };

        commentRepository
            .Setup(repo => repo.QueryByIdAsync(deletedComment.Id))
            .ReturnsAsync(deletedComment);

        var service = CreateService(commentRepository);

        var result = await service.GetCommentNavigationAsync(deletedComment.PostId, deletedComment.Id, 20, 5);

        result.ShouldBeNull();
    }

    [Fact(DisplayName = "帖子不匹配时应返回空")]
    public async Task GetCommentNavigationAsync_ShouldReturnNull_WhenPostMismatch()
    {
        var commentRepository = new Mock<IBaseRepository<Comment>>();
        var targetComment = new Comment
        {
            Id = 9201,
            PostId = 7201,
            IsDeleted = false,
            IsEnabled = true
        };

        commentRepository
            .Setup(repo => repo.QueryByIdAsync(targetComment.Id))
            .ReturnsAsync(targetComment);

        var service = CreateService(commentRepository);

        var result = await service.GetCommentNavigationAsync(9999, targetComment.Id, 20, 5);

        result.ShouldBeNull();
    }

    private static CommentService CreateService(Mock<IBaseRepository<Comment>> commentRepository)
    {
        var mapper = new Mock<IMapper>();
        var userCommentLikeRepository = new Mock<IBaseRepository<UserCommentLike>>();
        var highlightRepository = new Mock<IBaseRepository<CommentHighlight>>();
        var commentEditHistoryRepository = new Mock<IBaseRepository<CommentEditHistory>>();
        var postService = new Mock<IPostService>();
        var caching = new Mock<ICaching>();
        var coinRewardService = new Mock<ICoinRewardService>();
        var notificationService = new Mock<INotificationService>();
        var dedupService = new Mock<INotificationDedupService>();
        var experienceService = new Mock<IExperienceService>();
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>();

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
            Options.Create(new ForumEditHistoryOptions()));
    }
}
