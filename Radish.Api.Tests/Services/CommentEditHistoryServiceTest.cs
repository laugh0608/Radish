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
using Radish.Service;
using SqlSugar;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

/// <summary>
/// 评论编辑历史与限制规则测试
/// </summary>
public class CommentEditHistoryServiceTest
{
    [Fact(DisplayName = "普通用户超过次数上限应失败")]
    public async Task UpdateCommentAsync_ShouldFail_WhenReachMaxEditCount_ForNormalUser()
    {
        // Arrange
        var mapper = new Mock<IMapper>();
        var commentRepository = new Mock<IBaseRepository<Comment>>();
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

        var comment = new Comment
        {
            Id = 1001,
            PostId = 2001,
            AuthorId = 3001,
            AuthorName = "test-user",
            Content = "old-content",
            CreateTime = DateTime.Now.AddMinutes(-1),
            IsDeleted = false,
            TenantId = 1
        };

        commentRepository
            .Setup(r => r.QueryByIdAsync(comment.Id))
            .ReturnsAsync(comment);

        commentEditHistoryRepository
            .Setup(r => r.QueryCountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CommentEditHistory, bool>>?>()))
            .ReturnsAsync(2);

        var editOptions = new ForumEditHistoryOptions
        {
            Enable = true,
            Comment = new ForumCommentEditHistoryOptions
            {
                EnableHistory = true,
                MaxEditCount = 2,
                HistorySaveEditCount = 2,
                MaxHistoryRecords = 2,
                EditWindowMinutes = 5
            },
            AdminOverride = new ForumEditHistoryAdminOverrideOptions
            {
                BypassEditCountLimit = true,
                BypassCommentEditWindow = true
            }
        };

        var highlightOptions = new CommentHighlightOptions();

        var service = new CommentService(
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
            Options.Create(highlightOptions),
            commentEditHistoryRepository.Object,
            Options.Create(editOptions));

        // Act
        var (success, message) = await service.UpdateCommentAsync(comment.Id, "new-content", comment.AuthorId, "test-user", isAdmin: false);

        // Assert
        success.ShouldBeFalse();
        message.ShouldContain("编辑次数已达上限");
        commentRepository.Verify(r => r.UpdateAsync(It.IsAny<Comment>()), Times.Never);
    }

    [Fact(DisplayName = "历史保存次数不足时仍应按编辑次数上限拦截")]
    public async Task UpdateCommentAsync_ShouldFail_WhenEditCountReachedEvenIfHistoryCountLower()
    {
        var mapper = new Mock<IMapper>();
        var commentRepository = new Mock<IBaseRepository<Comment>>();
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

        var comment = new Comment
        {
            Id = 1003,
            PostId = 2003,
            AuthorId = 3003,
            AuthorName = "test-user",
            Content = "old-content",
            CreateTime = DateTime.Now.AddMinutes(-1),
            IsDeleted = false,
            TenantId = 1,
            EditCount = 3
        };

        commentRepository
            .Setup(r => r.QueryByIdAsync(comment.Id))
            .ReturnsAsync(comment);

        commentEditHistoryRepository
            .Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<CommentEditHistory, bool>>?>()))
            .ReturnsAsync(1);

        var editOptions = new ForumEditHistoryOptions
        {
            Enable = true,
            Comment = new ForumCommentEditHistoryOptions
            {
                EnableHistory = true,
                MaxEditCount = 3,
                HistorySaveEditCount = 1,
                MaxHistoryRecords = 1,
                EditWindowMinutes = 5
            },
            AdminOverride = new ForumEditHistoryAdminOverrideOptions
            {
                BypassEditCountLimit = true,
                BypassCommentEditWindow = true
            }
        };

        var service = new CommentService(
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
            Options.Create(editOptions));

        var (success, message) = await service.UpdateCommentAsync(comment.Id, "new-content", comment.AuthorId, "test-user", isAdmin: false);

        success.ShouldBeFalse();
        message.ShouldContain("编辑次数已达上限");
        commentRepository.Verify(r => r.UpdateAsync(It.IsAny<Comment>()), Times.Never);
    }

    [Fact(DisplayName = "管理员可越过次数与时间窗口限制")] 
    public async Task UpdateCommentAsync_ShouldSucceed_WhenAdminBypassEnabled()
    {
        // Arrange
        var mapper = new Mock<IMapper>();
        var commentRepository = new Mock<IBaseRepository<Comment>>();
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

        var comment = new Comment
        {
            Id = 1002,
            PostId = 2002,
            AuthorId = 3002,
            AuthorName = "original-author",
            Content = "old-content",
            CreateTime = DateTime.Now.AddMinutes(-30),
            IsDeleted = false,
            TenantId = 1
        };

        commentRepository
            .Setup(r => r.QueryByIdAsync(comment.Id))
            .ReturnsAsync(comment);

        commentRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Comment>()))
            .ReturnsAsync(true);

        commentEditHistoryRepository
            .Setup(r => r.QueryCountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CommentEditHistory, bool>>?>()))
            .ReturnsAsync(100);

        commentEditHistoryRepository
            .Setup(r => r.AddAsync(It.IsAny<CommentEditHistory>()))
            .ReturnsAsync(1L);

        commentEditHistoryRepository
            .Setup(r => r.QueryWithOrderAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<CommentEditHistory, bool>>?>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<CommentEditHistory, object>>>(),
                It.IsAny<SqlSugar.OrderByType>(),
                It.IsAny<int>()))
            .ReturnsAsync(new List<CommentEditHistory>());

        var editOptions = new ForumEditHistoryOptions
        {
            Enable = true,
            Comment = new ForumCommentEditHistoryOptions
            {
                EnableHistory = true,
                MaxEditCount = 1,
                HistorySaveEditCount = 1,
                MaxHistoryRecords = 1,
                EditWindowMinutes = 5
            },
            AdminOverride = new ForumEditHistoryAdminOverrideOptions
            {
                BypassEditCountLimit = true,
                BypassCommentEditWindow = true
            }
        };

        var highlightOptions = new CommentHighlightOptions();

        var service = new CommentService(
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
            Options.Create(highlightOptions),
            commentEditHistoryRepository.Object,
            Options.Create(editOptions));

        // Act
        var (success, message) = await service.UpdateCommentAsync(comment.Id, "new-content", comment.AuthorId, "admin-user", isAdmin: true);

        // Assert
        success.ShouldBeTrue();
        message.ShouldBe("编辑成功");
        commentRepository.Verify(r => r.UpdateAsync(It.IsAny<Comment>()), Times.Once);
    }
}
