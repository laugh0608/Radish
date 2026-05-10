using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Shouldly;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public class ContentModerationServiceTest
{
    [Fact]
    public async Task SubmitReportAsync_Should_Persist_Comment_Target_Snapshot_On_Create()
    {
        var contentReportRepository = new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict);
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var commentRepository = new Mock<IBaseRepository<Comment>>(MockBehavior.Strict);
        var service = CreateService(
            contentReportRepository: contentReportRepository,
            postRepository: postRepository,
            commentRepository: commentRepository);

        commentRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
            .ReturnsAsync(new Comment
            {
                Id = 61001,
                PostId = 51001,
                AuthorId = 31001,
                AuthorName = "comment-author",
                Content = "创建时评论内容"
            });
        postRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(new Post
            {
                Id = 51001,
                Title = "创建时帖子标题"
            });
        contentReportRepository
            .Setup(repository => repository.QueryExistsAsync(It.IsAny<Expression<Func<ContentReport, bool>>>()))
            .ReturnsAsync(false);

        ContentReport? createdReport = null;
        contentReportRepository
            .Setup(repository => repository.AddAsync(It.IsAny<ContentReport>()))
            .Callback<ContentReport>(report => createdReport = report)
            .ReturnsAsync(71001);

        var result = await service.SubmitReportAsync(
            new SubmitContentReportDto
            {
                TargetType = "Comment",
                TargetContentId = 61001,
                ReasonType = "Abuse",
                ReasonDetail = "需要快照固化"
            },
            41001,
            "reporter",
            1);

        result.ShouldBe(71001);
        createdReport.ShouldNotBeNull();
        createdReport!.TargetUserId.ShouldBe(31001);
        createdReport.TargetUserName.ShouldBe("comment-author");
        createdReport.TargetSnapshotPostId.ShouldBe(51001);
        createdReport.TargetSnapshotChannelId.ShouldBeNull();
        createdReport.TargetSnapshotTitle.ShouldBe("创建时帖子标题");
        createdReport.TargetSnapshotSummary.ShouldBe("创建时评论内容");
    }

    [Fact]
    public async Task GetReportQueueAsync_Should_Populate_Chat_Target_Navigation_For_Recalled_Message()
    {
        var contentReportRepository = new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict);
        var channelMessageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var service = CreateService(contentReportRepository: contentReportRepository, channelMessageRepository: channelMessageRepository);

        contentReportRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<ContentReport, bool>>?>(),
                1,
                20,
                It.IsAny<Expression<Func<ContentReport, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((
                new List<ContentReport>
                {
                    new()
                    {
                        Id = 70001,
                        ReportTargetType = (int)ContentReportTargetTypeEnum.ChatMessage,
                        TargetContentId = 90002,
                        TargetUserId = 10002,
                        TargetUserName = "target",
                        ReporterUserId = 10001,
                        ReporterUserName = "reporter",
                        ReasonType = "Spam",
                        Status = (int)ContentReportStatusEnum.Pending,
                        ReviewActionType = (int)ModerationActionTypeEnum.None,
                        CreateTime = new DateTime(2026, 5, 10, 12, 0, 0)
                    }
                },
                1));
        channelMessageRepository
            .Setup(repository => repository.QueryByIdsIncludingDeletedAsync(It.Is<List<long>>(ids => ids.Count == 1 && ids[0] == 90002)))
            .ReturnsAsync(new List<ChannelMessage>
            {
                new()
                {
                    Id = 90002,
                    ChannelId = 88,
                    UserId = 10002,
                    UserName = "target",
                    Type = MessageType.Text,
                    Content = null,
                    IsDeleted = true,
                    CreateTime = new DateTime(2026, 5, 10, 11, 50, 0)
                }
            });

        var result = await service.GetReportQueueAsync(null, 1, 20);

        result.VoItems.Count.ShouldBe(1);
        var reportItem = result.VoItems[0];
        reportItem.VoTargetType.ShouldBe("ChatMessage");
        reportItem.VoTargetChannelId.ShouldBe(88);
        reportItem.VoTargetMessageId.ShouldBe(90002);
    }

    [Fact]
    public async Task GetReportQueueAsync_Should_Prefer_Persisted_Chat_Snapshot_When_Current_Content_Changes()
    {
        var contentReportRepository = new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict);
        var channelMessageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var service = CreateService(contentReportRepository: contentReportRepository, channelMessageRepository: channelMessageRepository);

        contentReportRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<ContentReport, bool>>?>(),
                1,
                20,
                It.IsAny<Expression<Func<ContentReport, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((
                new List<ContentReport>
                {
                    new()
                    {
                        Id = 70004,
                        ReportTargetType = (int)ContentReportTargetTypeEnum.ChatMessage,
                        TargetContentId = 90005,
                        TargetSnapshotChannelId = 118,
                        TargetSnapshotSummary = "创建时聊天快照",
                        TargetUserId = 10005,
                        TargetUserName = "target",
                        ReporterUserId = 10001,
                        ReporterUserName = "reporter",
                        ReasonType = "Spam",
                        Status = (int)ContentReportStatusEnum.Pending,
                        ReviewActionType = (int)ModerationActionTypeEnum.None,
                        CreateTime = new DateTime(2026, 5, 10, 12, 20, 0)
                    }
                },
                1));
        channelMessageRepository
            .Setup(repository => repository.QueryByIdsIncludingDeletedAsync(It.Is<List<long>>(ids => ids.Count == 1 && ids[0] == 90005)))
            .ReturnsAsync(new List<ChannelMessage>
            {
                new()
                {
                    Id = 90005,
                    ChannelId = 118,
                    UserId = 10005,
                    UserName = "target",
                    Type = MessageType.Text,
                    Content = "当前消息内容已经变化",
                    IsDeleted = false,
                    CreateTime = new DateTime(2026, 5, 10, 12, 19, 0)
                }
            });

        var result = await service.GetReportQueueAsync(null, 1, 20);

        result.VoItems.Count.ShouldBe(1);
        var reportItem = result.VoItems[0];
        reportItem.VoTargetChannelId.ShouldBe(118);
        reportItem.VoTargetSnapshotSummary.ShouldBe("创建时聊天快照");
    }

    [Fact]
    public async Task ReviewReportAsync_Should_Return_Chat_Target_Navigation_When_Report_Target_Is_ChatMessage()
    {
        var contentReportRepository = new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict);
        var channelMessageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var service = CreateService(contentReportRepository: contentReportRepository, channelMessageRepository: channelMessageRepository);

        var report = new ContentReport
        {
            Id = 70002,
            ReportTargetType = (int)ContentReportTargetTypeEnum.ChatMessage,
            TargetContentId = 90003,
            TargetUserId = 10003,
            TargetUserName = "target",
            ReporterUserId = 10001,
            ReporterUserName = "reporter",
            ReasonType = "Abuse",
            Status = (int)ContentReportStatusEnum.Pending,
            ReviewActionType = (int)ModerationActionTypeEnum.None,
            CreateTime = new DateTime(2026, 5, 10, 12, 5, 0)
        };

        contentReportRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<ContentReport, bool>>?>()))
            .ReturnsAsync(report);
        contentReportRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<ContentReport>()))
            .ReturnsAsync(true);
        channelMessageRepository
            .Setup(repository => repository.QueryByIdsIncludingDeletedAsync(It.Is<List<long>>(ids => ids.Count == 1 && ids[0] == 90003)))
            .ReturnsAsync(new List<ChannelMessage>
            {
                new()
                {
                    Id = 90003,
                    ChannelId = 99,
                    UserId = 10003,
                    UserName = "target",
                    Type = MessageType.Text,
                    Content = "hello",
                    IsDeleted = false,
                    CreateTime = new DateTime(2026, 5, 10, 11, 55, 0)
                }
            });

        var result = await service.ReviewReportAsync(
            new ReviewContentReportDto
            {
                ReportId = 70002,
                IsApproved = false,
                ActionType = (int)ModerationActionTypeEnum.None,
                DurationHours = null,
                ReviewRemark = "not enough evidence"
            },
            20001,
            "reviewer",
            0);

        result.VoReportId.ShouldBe(70002);
        result.VoTargetChannelId.ShouldBe(99);
        result.VoTargetMessageId.ShouldBe(90003);
        result.VoStatus.ShouldBe("Rejected");
    }

    [Fact]
    public async Task GetActionLogsAsync_Should_Populate_Chat_Source_Report_Navigation_For_Action_Log()
    {
        var contentReportRepository = new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict);
        var moderationActionRepository = new Mock<IBaseRepository<UserModerationAction>>(MockBehavior.Strict);
        var channelMessageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var service = CreateService(
            contentReportRepository: contentReportRepository,
            moderationActionRepository: moderationActionRepository,
            channelMessageRepository: channelMessageRepository);

        moderationActionRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<UserModerationAction, bool>>?>(),
                1,
                20,
                It.IsAny<Expression<Func<UserModerationAction, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((
                new List<UserModerationAction>
                {
                    new()
                    {
                        Id = 81001,
                        TargetUserId = 10002,
                        TargetUserName = "target",
                        ActionType = (int)ModerationActionTypeEnum.Mute,
                        Reason = "举报审核通过：Abuse",
                        SourceReportId = 70003,
                        DurationHours = 24,
                        StartTime = new DateTime(2026, 5, 10, 12, 10, 0),
                        EndTime = new DateTime(2026, 5, 11, 12, 10, 0),
                        IsActive = true,
                        CreateId = 20001,
                        CreateBy = "reviewer",
                        CreateTime = new DateTime(2026, 5, 10, 12, 10, 0)
                    }
                },
                1));
        contentReportRepository
            .Setup(repository => repository.QueryByIdsAsync(It.Is<List<long>>(ids => ids.Count == 1 && ids[0] == 70003)))
            .ReturnsAsync(new List<ContentReport>
            {
                new()
                {
                    Id = 70003,
                    ReportTargetType = (int)ContentReportTargetTypeEnum.ChatMessage,
                    TargetContentId = 90004,
                    TargetUserId = 10002,
                    TargetUserName = "target",
                    ReporterUserId = 10001,
                    ReporterUserName = "reporter",
                    ReasonType = "Abuse",
                    Status = (int)ContentReportStatusEnum.Approved,
                    ReviewActionType = (int)ModerationActionTypeEnum.Mute,
                    CreateTime = new DateTime(2026, 5, 10, 12, 0, 0)
                }
            });
        channelMessageRepository
            .Setup(repository => repository.QueryByIdsIncludingDeletedAsync(It.Is<List<long>>(ids => ids.Count == 1 && ids[0] == 90004)))
            .ReturnsAsync(new List<ChannelMessage>
            {
                new()
                {
                    Id = 90004,
                    ChannelId = 108,
                    UserId = 10002,
                    UserName = "target",
                    Type = MessageType.Text,
                    Content = "flagged",
                    IsDeleted = true,
                    CreateTime = new DateTime(2026, 5, 10, 11, 59, 0)
                }
            });

        var result = await service.GetActionLogsAsync(1, 20);

        result.VoItems.Count.ShouldBe(1);
        var action = result.VoItems[0];
        action.VoActionId.ShouldBe(81001);
        action.VoSourceReportId.ShouldBe(70003);
        action.VoSourceReportTargetType.ShouldBe("ChatMessage");
        action.VoSourceReportTargetContentId.ShouldBe(90004);
        action.VoSourceReportTargetChannelId.ShouldBe(108);
        action.VoSourceReportTargetMessageId.ShouldBe(90004);
    }

    private static ContentModerationService CreateService(
        Mock<IBaseRepository<ContentReport>>? contentReportRepository = null,
        Mock<IBaseRepository<UserModerationAction>>? moderationActionRepository = null,
        Mock<IBaseRepository<Post>>? postRepository = null,
        Mock<IBaseRepository<Comment>>? commentRepository = null,
        Mock<IChannelMessageRepository>? channelMessageRepository = null)
    {
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var basePostRepository = postRepository ?? new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var baseCommentRepository = commentRepository ?? new Mock<IBaseRepository<Comment>>(MockBehavior.Strict);
        var productRepository = new Mock<IBaseRepository<Product>>(MockBehavior.Strict);
        var postQuickReplyRepository = new Mock<IBaseRepository<PostQuickReply>>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);

        return new ContentModerationService(
            mapper.Object,
            (contentReportRepository ?? new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict)).Object,
            (moderationActionRepository ?? new Mock<IBaseRepository<UserModerationAction>>(MockBehavior.Strict)).Object,
            basePostRepository.Object,
            baseCommentRepository.Object,
            (channelMessageRepository ?? new Mock<IChannelMessageRepository>(MockBehavior.Strict)).Object,
            productRepository.Object,
            postQuickReplyRepository.Object,
            userRepository.Object);
    }
}
